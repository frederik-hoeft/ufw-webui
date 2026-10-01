using System.Text.Json;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Persistence;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FileReorderRecoveryJournal(IConfiguration configuration, IDurableFileStore durableFiles) : IReorderRecoveryJournal
{
    public async Task<ReorderRecoveryJournalEntry?> ReadAsync(CancellationToken cancellationToken)
    {
        string path = GetPath();
        if (!File.Exists(path))
        {
            return null;
        }

        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        ReorderRecoveryJournalEntry entry = await JsonSerializer.DeserializeAsync(stream, ReorderRecoveryJsonSerializerContext.Default.ReorderRecoveryJournalEntry, cancellationToken)
            ?? throw new InvalidDataException("Reorder recovery journal is empty.");
        Validate(entry);
        return entry;
    }

    public async Task WriteAsync(ReorderRecoveryJournalEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(entry, ReorderRecoveryJsonSerializerContext.Default.ReorderRecoveryJournalEntry);
        byte[] contents = new byte[json.Length + 1];
        json.CopyTo(contents, 0);
        contents[^1] = (byte)'\n';
        await durableFiles.ReplaceAsync(GetPath(), contents, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        durableFiles.Delete(GetPath());
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
        return !string.IsNullOrWhiteSpace(path) ? path : throw new InvalidOperationException("Reorder recovery journal path is not configured.");
    }
}
