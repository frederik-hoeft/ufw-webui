using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Ufw.Shared.Firewall;

/// <summary>
/// Content-addressed identity of a firewall rule. Two rules with the same
/// identity are semantically the same match/action regardless of UFW numbering
/// or comment text.
/// </summary>
public static class RuleIdentity
{
    public const string PREFIX = "sha256:";

    public static string Compute(FirewallRuleSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(specification);
        string canonical = Canonicalize(normalized);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return PREFIX + Base64Url.EncodeToString(hash);
    }

    private static string Canonicalize(FirewallRuleSpecification specification)
    {
        StringBuilder builder = new();
        builder.Append("rule-identity/2\n");
        AppendField(builder, "action", RuleSpecificationNormalizer.FormatAction(specification.Action));
        AppendField(builder, "addressFamily", RuleSpecificationNormalizer.FormatAddressFamily(specification.AddressFamily));
        AppendField(builder, "destination", specification.Destination ?? RuleSpecificationNormalizer.ANY);
        AppendField(builder, "destinationInterface", specification.DestinationInterface ?? string.Empty);
        AppendField(builder, "destinationPorts", specification.DestinationPorts ?? string.Empty);
        AppendField(builder, "direction", RuleSpecificationNormalizer.FormatDirection(specification.Direction));
        AppendField(builder, "protocol", RuleSpecificationNormalizer.FormatProtocol(specification.Protocol));
        AppendField(builder, "source", specification.Source ?? RuleSpecificationNormalizer.ANY);
        AppendField(builder, "sourceInterface", specification.SourceInterface ?? string.Empty);
        AppendField(builder, "sourcePorts", specification.SourcePorts ?? string.Empty);
        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string name, string value)
    {
        builder.Append(name);
        builder.Append('=');
        builder.Append(value);
        builder.Append('\n');
    }

    public static bool AreEqual(FirewallRuleSpecification left, FirewallRuleSpecification right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return string.Equals(Compute(left), Compute(right), StringComparison.Ordinal);
    }
}
