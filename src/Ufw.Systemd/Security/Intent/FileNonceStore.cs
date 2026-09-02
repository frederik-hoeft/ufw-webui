using System.Globalization;
using System.Text;
using Ufw.Ipc.Shared.Threading;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Security.Intent;

internal sealed class FileNonceStore : INonceStore, IDisposable
{
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
        return await _lock.RunTaskAsync(ct => ConsumeUnsynchronizedAsync(nonce, expiresAtUnix, ct), cancellationToken);
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
        await AppendAsync(nonce, expiresAtUnix, cancellationToken);
        return true;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        string? path = _configuration.Settings.Security?.NonceStorePath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            string[] lines = await File.ReadAllLinesAsync(path, cancellationToken);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                string[] parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2
                    || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiresAt))
                {
                    continue;
                }

                _consumed.Add(parts[0]);
                _expirations[parts[0]] = expiresAt;
            }
        }

        PruneExpired();
        await RewriteAsync(cancellationToken);
        _loaded = true;
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
        string? path = _configuration.Settings.Security?.NonceStorePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

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
        await stream.FlushAsync(cancellationToken);
    }

    private async Task RewriteAsync(CancellationToken cancellationToken)
    {
        string? path = _configuration.Settings.Security?.NonceStorePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        StringBuilder builder = new();
        builder.AppendLine("# ufw-intent-nonces v1");
        foreach ((string nonce, long expiresAt) in _expirations.OrderBy(static pair => pair.Value))
        {
            builder.Append(nonce);
            builder.Append(' ');
            builder.Append(expiresAt.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine();
        }

        string temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, builder.ToString(), cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
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
