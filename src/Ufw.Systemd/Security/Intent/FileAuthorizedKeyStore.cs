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
    private const string BEGIN_PUBLIC_KEY = "-----BEGIN PUBLIC KEY-----";
    private const string END_PUBLIC_KEY = "-----END PUBLIC KEY-----";

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
        List<string> pemBlocks = ExtractPemBlocks(contents);
        Dictionary<string, ECDsa> loaded = new(StringComparer.Ordinal);
        try
        {
            foreach (string pem in pemBlocks)
            {
                ECDsa? key = ECDsa.Create();
                try
                {
                    try
                    {
                        key.ImportFromPem(pem);
                    }
                    catch (Exception exception) when (exception is CryptographicException or ArgumentException or FormatException)
                    {
                        throw new InvalidDataException("Authorized keys file contains an unreadable public key.", exception);
                    }

                    if (!IntentSigner.IsP256(key))
                    {
                        throw new InvalidDataException("Authorized intent keys must be ECDSA P-256 public keys.");
                    }

                    string keyId = IntentSigner.ComputeKeyId(key);
                    if (loaded.TryAdd(keyId, key))
                    {
                        key = null;
                    }
                }
                finally
                {
                    key?.Dispose();
                }
            }

            _logger.LogInformation($"Loaded {loaded.Count} authorized intent public key(s).");
            return loaded.ToFrozenDictionary(StringComparer.Ordinal);
        }
        catch
        {
            foreach (ECDsa key in loaded.Values)
            {
                key.Dispose();
            }

            throw;
        }
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
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                if (!string.Equals(trimmed, BEGIN_PUBLIC_KEY, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Authorized keys file may contain only PUBLIC KEY PEM blocks and comments.");
                }

                current = new StringBuilder();
                current.AppendLine(trimmed);
                continue;
            }

            if (trimmed.StartsWith("-----BEGIN ", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Authorized keys file contains a nested PEM block.");
            }

            current.AppendLine(trimmed);
            if (trimmed.StartsWith("-----END ", StringComparison.Ordinal))
            {
                if (!string.Equals(trimmed, END_PUBLIC_KEY, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Authorized keys file contains a mismatched PEM block.");
                }

                blocks.Add(current.ToString());
                current = null;
            }
        }

        if (current is not null)
        {
            throw new InvalidDataException("Authorized keys file contains an unterminated PEM block.");
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
