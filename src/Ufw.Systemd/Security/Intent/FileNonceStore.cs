using System.Globalization;
using System.Text;
using Ufw.Shared.Threading;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Persistence;

namespace Ufw.Systemd.Security.Intent;

internal sealed class FileNonceStore(IConfiguration configuration, TimeProvider timeProvider, IDurableFileStore durableFiles) : INonceStore, IDisposable
{
    private const string HEADER = "# ufw-intent-nonces v1";
    private const int COMPACTION_STALE_RECORD_THRESHOLD = 64;

    private readonly IConfiguration _configuration = configuration;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IDurableFileStore _durableFiles = durableFiles;
    private readonly AsyncLock _lock = new();
    private readonly Dictionary<string, long> _expirations = new(StringComparer.Ordinal);
    private int _staleRecordCount;
    private bool _loaded;
    private bool _disposed;

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
        await CompactIfNeededAsync();
        if (_expirations.ContainsKey(nonce))
        {
            return false;
        }

        await AppendAsync(nonce, expiresAtUnix, CancellationToken.None);
        _expirations[nonce] = expiresAtUnix;
        return true;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        _expirations.Clear();
        _staleRecordCount = 0;
        string path = GetStorePath();
        if (!File.Exists(path))
        {
            await RewriteAsync(CancellationToken.None);
            _loaded = true;
            return;
        }

        string[] lines = await File.ReadAllLinesAsync(path, cancellationToken);
        Dictionary<string, long> loadedExpirations = new(StringComparer.Ordinal);
        int recordCount = Parse(lines, loadedExpirations);
        foreach ((string nonce, long expiresAt) in loadedExpirations)
        {
            _expirations.Add(nonce, expiresAt);
        }

        PruneExpired();
        _staleRecordCount = recordCount - _expirations.Count;
        if (_staleRecordCount > 0)
        {
            await RewriteAsync(CancellationToken.None);
        }

        _loaded = true;
    }

    private static int Parse(string[] lines, Dictionary<string, long> expirations)
    {
        bool headerSeen = false;
        int recordCount = 0;
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
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiresAt))
            {
                throw new InvalidDataException("Intent replay store contains a malformed nonce record.");
            }

            expirations[parts[0]] = expiresAt;
            recordCount++;
        }

        if (!headerSeen)
        {
            throw new InvalidDataException("Intent replay store is missing its header.");
        }

        return recordCount;
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
            _expirations.Remove(nonce);
        }

        _staleRecordCount += expired.Count;
    }

    private async Task CompactIfNeededAsync()
    {
        if (_staleRecordCount >= COMPACTION_STALE_RECORD_THRESHOLD)
        {
            await RewriteAsync(CancellationToken.None);
        }
    }

    private Task AppendAsync(string nonce, long expiresAtUnix, CancellationToken cancellationToken)
    {
        string line = nonce + " " + expiresAtUnix.ToString(CultureInfo.InvariantCulture) + Environment.NewLine;
        return _durableFiles.AppendAsync(GetStorePath(), Encoding.UTF8.GetBytes(line), cancellationToken);
    }

    private async Task RewriteAsync(CancellationToken cancellationToken)
    {
        StringBuilder builder = new();
        builder.AppendLine(HEADER);
        IEnumerable<KeyValuePair<string, long>> ordered = _expirations.OrderBy(static pair => pair.Value).ThenBy(static pair => pair.Key, StringComparer.Ordinal);
        foreach ((string nonce, long expiresAt) in ordered)
        {
            builder.Append(nonce);
            builder.Append(' ');
            builder.Append(expiresAt.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        await _durableFiles.ReplaceAsync(GetStorePath(), Encoding.UTF8.GetBytes(builder.ToString()), cancellationToken);
        _staleRecordCount = 0;
    }

    private string GetStorePath()
    {
        string? path = _configuration.Settings.Security?.NonceStorePath;
        return !string.IsNullOrWhiteSpace(path) ? path : throw new InvalidOperationException("Intent replay store path is not configured.");
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
