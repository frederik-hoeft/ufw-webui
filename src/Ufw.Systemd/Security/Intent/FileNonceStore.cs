using System.Globalization;
using System.Text;
using Ufw.Ipc.Shared.Threading;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Security.Intent;

internal sealed class FileNonceStore : INonceStore, IDisposable
{
    private const string HEADER = "# ufw-intent-nonces v1";

    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly AsyncLock _lock = new();
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _expirations = new(StringComparer.Ordinal);
    private bool _loaded;
    private bool _disposed;

    public FileNonceStore(IConfiguration configuration, TimeProvider timeProvider)
    {
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async ValueTask<bool> TryConsumeAsync(string nonce, long expiresAtUnix, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);
        try
        {
            return await _lock.RunTaskAsync(ct => ConsumeUnsynchronizedAsync(nonce, expiresAtUnix, ct), cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException("Intent replay state could not be persisted.", ex);
        }
    }

    private async Task<bool> ConsumeUnsynchronizedAsync(string nonce, long expiresAtUnix, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        PruneExpired();
        if (!_consumed.Add(nonce))
        {
            return false;
        }

        _expirations[nonce] = expiresAtUnix;
        await AppendAsync(nonce, expiresAtUnix, CancellationToken.None);
        return true;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        string path = GetStorePath();
        if (File.Exists(path))
        {
            string[] lines = await File.ReadAllLinesAsync(path, cancellationToken);
            Parse(lines);
        }

        PruneExpired();
        await RewriteAsync(CancellationToken.None);
        _loaded = true;
    }

    private void Parse(string[] lines)
    {
        bool headerSeen = false;
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!headerSeen)
            {
                if (!string.Equals(line, HEADER, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Intent replay store has an invalid or unsupported header.");
                }

                headerSeen = true;
                continue;
            }

            if (line.StartsWith('#'))
            {
                throw new InvalidDataException("Intent replay store contains unexpected metadata.");
            }

            string[] parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2
                || string.IsNullOrWhiteSpace(parts[0])
                || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiresAt))
            {
                throw new InvalidDataException("Intent replay store contains a malformed nonce record.");
            }

            _consumed.Add(parts[0]);
            _expirations[parts[0]] = expiresAt;
        }

        if (!headerSeen)
        {
            throw new InvalidDataException("Intent replay store is missing its header.");
        }
    }

    private void PruneExpired()
    {
        long now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        List<string> expired = [];
        foreach ((string nonce, long expiresAt) in _expirations)
        {
            if (expiresAt <= now)
            {
                expired.Add(nonce);
            }
        }

        foreach (string nonce in expired)
        {
            _consumed.Remove(nonce);
            _expirations.Remove(nonce);
        }
    }

    private async Task AppendAsync(string nonce, long expiresAtUnix, CancellationToken cancellationToken)
    {
        string path = GetStorePath();
        EnsureParentDirectory(path);

        string line = nonce + " " + expiresAtUnix.ToString(CultureInfo.InvariantCulture) + Environment.NewLine;
        byte[] bytes = Encoding.UTF8.GetBytes(line);
        await using FileStream stream = new(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
#pragma warning disable CA1849 // Flush(bool) is intentionally synchronous to guarantee durable replay-state persistence.
        stream.Flush(flushToDisk: true);
#pragma warning restore CA1849
    }

    private async Task RewriteAsync(CancellationToken cancellationToken)
    {
        string path = GetStorePath();
        EnsureParentDirectory(path);

        StringBuilder builder = new();
        builder.AppendLine(HEADER);
        IEnumerable<KeyValuePair<string, long>> ordered = _expirations
            .OrderBy(static pair => pair.Value)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal);
        foreach ((string nonce, long expiresAt) in ordered)
        {
            builder.Append(nonce);
            builder.Append(' ');
            builder.Append(expiresAt.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        string temporaryPath = path + ".tmp";
        byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
        await using (FileStream stream = new(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes, cancellationToken);
#pragma warning disable CA1849 // Flush(bool) is intentionally synchronous to guarantee durable replay-state persistence.
            stream.Flush(flushToDisk: true);
#pragma warning restore CA1849
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    private string GetStorePath()
    {
        string? path = _configuration.Settings.Security?.NonceStorePath;
        return !string.IsNullOrWhiteSpace(path)
            ? path
            : throw new InvalidOperationException("Intent replay store path is not configured.");
    }

    private static void EnsureParentDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lock.Dispose();
    }
}
