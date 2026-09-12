using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Shared.Firewall;

/// <summary>
/// Computes the versioned fingerprint that binds state-dependent mutations to an exact listed firewall snapshot.
/// </summary>
public static class FirewallRuleSnapshotFingerprint
{
    public const string PREFIX = "sha256:";

    private const string CONTEXT = "ufw-webui/firewall-rule-snapshot/1";

    public static string Compute(RuleListResponse snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Compute(snapshot.Active, snapshot.Rules);
    }

    public static bool IsValid(string? fingerprint)
    {
        if (fingerprint is null || !fingerprint.StartsWith(PREFIX, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            byte[] digest = Base64Url.DecodeFromChars(fingerprint.AsSpan(PREFIX.Length));
            return digest.Length == SHA256.HashSizeInBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Compute(bool active, IReadOnlyList<ListedFirewallRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        CanonicalHashWriter writer = new(hash);
        writer.WriteString(CONTEXT);
        writer.WriteBoolean(active);
        writer.WriteInt32(rules.Count);

        for (int index = 0; index < rules.Count; index++)
        {
            ListedFirewallRule rule = rules[index] ?? throw new ArgumentException("Snapshot rules cannot contain null entries.", nameof(rules));
            WriteRule(writer, index, rule);
        }

        return PREFIX + Base64Url.EncodeToString(hash.GetHashAndReset());
    }

    private static void WriteRule(CanonicalHashWriter writer, int index, ListedFirewallRule listedRule)
    {
        writer.WriteInt32(index);
        writer.WriteNullableInt32(listedRule.DisplayNumber);
        writer.WriteBoolean(listedRule.Parsed);
        writer.WriteNullableString(listedRule.RawLine);
        writer.WriteNullableString(listedRule.RuleId);
        writer.WriteBoolean(listedRule.Rule is not null);
        if (listedRule.Rule is null)
        {
            return;
        }

        FirewallRuleSpecification rule = listedRule.Rule;
        writer.WriteString(RuleSpecificationNormalizer.FormatAction(rule.Action));
        writer.WriteString(RuleSpecificationNormalizer.FormatAddressFamily(rule.AddressFamily));
        writer.WriteString(RuleSpecificationNormalizer.FormatDirection(rule.Direction));
        writer.WriteString(RuleSpecificationNormalizer.FormatProtocol(rule.Protocol));
        writer.WriteNullableString(rule.Source);
        writer.WriteNullableString(rule.SourcePorts);
        writer.WriteNullableString(rule.SourceInterface);
        writer.WriteNullableString(rule.Destination);
        writer.WriteNullableString(rule.DestinationPorts);
        writer.WriteNullableString(rule.DestinationInterface);
        writer.WriteNullableString(rule.Comment);
    }

    private sealed class CanonicalHashWriter(IncrementalHash hash)
    {
        private readonly IncrementalHash _hash = hash;

        public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteInt32(int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            _hash.AppendData(bytes);
        }

        public void WriteNullableInt32(int? value)
        {
            WriteBoolean(value.HasValue);
            if (value.HasValue)
            {
                WriteInt32(value.Value);
            }
        }

        public void WriteNullableString(string? value)
        {
            WriteBoolean(value is not null);
            if (value is not null)
            {
                WriteString(value);
            }
        }

        public void WriteString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteInt32(bytes.Length);
            _hash.AppendData(bytes);
        }

        private void WriteByte(byte value)
        {
            Span<byte> bytes = stackalloc byte[1];
            bytes[0] = value;
            _hash.AppendData(bytes);
        }
    }
}
