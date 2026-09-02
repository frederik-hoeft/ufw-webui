using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Security.Intent;

internal sealed class FileAuthorizedKeyStore : IAuthorizedKeyStore, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileAuthorizedKeyStore> _logger;
    private readonly object _sync = new();
    private FrozenDictionary<string, ECDsa>? _keys;
    private bool _disposed;

    public FileAuthorizedKeyStore(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger.Scoped(this);
    }

    public bool TryGetKey(string keyId, [NotNullWhen(true)] out ECDsa? key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        FrozenDictionary<string, ECDsa> keys = GetOrLoadKeys();
        return keys.TryGetValue(keyId, out key);
    }

    private FrozenDictionary<string, ECDsa> GetOrLoadKeys()
    {
        FrozenDictionary<string, ECDsa>? keys = Volatile.Read(in _keys);
        if (keys is not null)
        {
            return keys;
        }

        lock (_sync)
        {
            keys = _keys;
            if (keys is not null)
            {
                return keys;
            }

            keys = LoadKeys();
            Volatile.Write(ref _keys, keys);
            return keys;
        }
    }

    private FrozenDictionary<string, ECDsa> LoadKeys()
    {
        string? path = _configuration.Settings.Security?.AuthorizedKeysPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning("Authorized keys file is missing; firewall mutations will be rejected.");
            return FrozenDictionary<string, ECDsa>.Empty;
        }

        string contents = File.ReadAllText(path);
        Dictionary<string, ECDsa> loaded = new(StringComparer.Ordinal);
        foreach (string pem in ExtractPemBlocks(contents))
        {
            ECDsa key = ECDsa.Create();
            try
            {
                key.ImportFromPem(pem);
                if (!IntentSigner.IsP256(key))
                {
                    _logger.LogWarning("Ignoring authorized key that is not ECDSA P-256.");
                    key.Dispose();
                    continue;
                }

                string keyId = IntentSigner.ComputeKeyId(key);
                if (!loaded.TryAdd(keyId, key))
                {
                    _logger.LogWarning($"Ignoring duplicate authorized key '{keyId}'.");
                    key.Dispose();
                }
            }
            catch (Exception exception) when (exception is CryptographicException or ArgumentException or FormatException)
            {
                key.Dispose();
                _logger.LogWarning(exception, "Ignoring unreadable authorized public key.");
            }
        }

        _logger.LogInformation($"Loaded {loaded.Count} authorized intent public key(s).");
        return loaded.ToFrozenDictionary(StringComparer.Ordinal);
    }

    internal static List<string> ExtractPemBlocks(string contents)
    {
        List<string> blocks = [];
        StringReader reader = new(contents);
        StringBuilder? current = null;
        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();
            if (current is null)
            {
                if (trimmed.StartsWith("-----BEGIN ", StringComparison.Ordinal) && trimmed.EndsWith("-----", StringComparison.Ordinal))
                {
                    current = new StringBuilder();
                    current.AppendLine(trimmed);
                }

                continue;
            }

            current.AppendLine(trimmed);
            if (trimmed.StartsWith("-----END ", StringComparison.Ordinal) && trimmed.EndsWith("-----", StringComparison.Ordinal))
            {
                blocks.Add(current.ToString());
                current = null;
            }
        }

        return blocks;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        FrozenDictionary<string, ECDsa>? keys = _keys;
        if (keys is null)
        {
            return;
        }

        foreach ((_, ECDsa key) in keys)
        {
            key.Dispose();
        }
    }
}
