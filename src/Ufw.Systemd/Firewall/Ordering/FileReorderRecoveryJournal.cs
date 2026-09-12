using System.Text.Json;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FileReorderRecoveryJournal(IConfiguration configuration) : IReorderRecoveryJournal
{
    public async Task<ReorderRecoveryJournalEntry?> ReadAsync(CancellationToken cancellationToken)
    {
        string path = GetPath();
        if (!File.Exists(path))
        {
            return null;
        }

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        ReorderRecoveryJournalEntry entry = await JsonSerializer.DeserializeAsync(
            stream,
            ReorderRecoveryJsonSerializerContext.Default.ReorderRecoveryJournalEntry,
            cancellationToken)
            ?? throw new InvalidDataException("Reorder recovery journal is empty.");
        Validate(entry);
        return entry;
    }

    public async Task WriteAsync(ReorderRecoveryJournalEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string path = GetPath();
        EnsureParentDirectory(path);
        string temporaryPath = path + ".tmp";

        await using (FileStream stream = new(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                entry,
                ReorderRecoveryJsonSerializerContext.Default.ReorderRecoveryJournalEntry,
                cancellationToken);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
#pragma warning disable CA1849 // Flush(bool) is intentionally synchronous to guarantee durable recovery-state persistence.
            stream.Flush(flushToDisk: true);
#pragma warning restore CA1849
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetPath();
        try
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException("Reorder recovery journal could not be cleared.", exception);
        }

        return Task.CompletedTask;
    }

    private static void Validate(ReorderRecoveryJournalEntry entry)
    {
        if (entry.FormatVersion != ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION)
        {
            throw new InvalidDataException($"Unsupported reorder recovery journal format version {entry.FormatVersion}.");
        }
        if (entry.Rule is null || entry.OriginalFamilyPosition <= 0 || entry.ExpectedMultiplicity <= 0)
        {
            throw new InvalidDataException("Reorder recovery journal contains invalid recovery state.");
        }
    }

    private string GetPath()
    {
        string? path = configuration.Settings.Security?.ReorderRecoveryJournalPath;
        return !string.IsNullOrWhiteSpace(path)
            ? path
            : throw new InvalidOperationException("Reorder recovery journal path is not configured.");
    }

    private static void EnsureParentDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
