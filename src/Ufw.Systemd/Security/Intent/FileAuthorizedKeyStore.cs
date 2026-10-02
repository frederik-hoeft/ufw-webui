using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Security.Intent;

internal sealed class FileAuthorizedKeyStore : IAuthorizedKeyStore
{
    private const string BEGIN_PUBLIC_KEY = "-----BEGIN PUBLIC KEY-----";
    private const string END_PUBLIC_KEY = "-----END PUBLIC KEY-----";

    private readonly ILogger<FileAuthorizedKeyStore> _logger;
    private readonly FrozenDictionary<string, byte[]> _keys;

    public FileAuthorizedKeyStore(IConfiguration configuration, ILogger logger)
    {
        _logger = logger.Scoped<FileAuthorizedKeyStore>();
        _keys = LoadKeys(configuration.Settings.Security?.AuthorizedKeysPath);
    }

    public AuthorizedKeyVerificationResult VerifySignature(string keyId, ReadOnlyMemory<byte> data, string signature)
    {
        if (!_keys.TryGetValue(keyId, out byte[]? subjectPublicKeyInfo))
        {
            return AuthorizedKeyVerificationResult.UnknownKey;
        }

        using ECDsa key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);
        return IntentSigner.Verify(key, data.Span, signature) ? AuthorizedKeyVerificationResult.Verified : AuthorizedKeyVerificationResult.InvalidSignature;
    }

    private FrozenDictionary<string, byte[]> LoadKeys(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning("Authorized keys file is missing; firewall mutations will be rejected.");
            return FrozenDictionary<string, byte[]>.Empty;
        }

        string contents = File.ReadAllText(path);
        List<string> pemBlocks = ExtractPemBlocks(contents);
        Dictionary<string, byte[]> loaded = new(StringComparer.Ordinal);
        foreach (string pem in pemBlocks)
        {
            using ECDsa key = ECDsa.Create();
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
            loaded.TryAdd(keyId, key.ExportSubjectPublicKeyInfo());
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
}
