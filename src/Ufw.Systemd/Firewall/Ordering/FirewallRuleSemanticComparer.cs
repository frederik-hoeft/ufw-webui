using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall.Ordering;

internal static class FirewallRuleSemanticComparer
{
    public static bool Equals(ListedFirewallRule left, ListedFirewallRule right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Parsed != right.Parsed)
        {
            return false;
        }

        if (!left.Parsed)
        {
            return string.Equals(NormalizeRawLine(left.RawLine), NormalizeRawLine(right.RawLine), StringComparison.Ordinal);
        }

        return left.Rule is not null && right.Rule is not null && Equals(left.Rule, right.Rule);
    }

    public static bool Equals(FirewallRuleSpecification left, FirewallRuleSpecification right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        FirewallRuleSpecification x = RuleSpecificationNormalizer.Normalize(left);
        FirewallRuleSpecification y = RuleSpecificationNormalizer.Normalize(right);
        return x.Action == y.Action
            && x.AddressFamily == y.AddressFamily
            && x.Direction == y.Direction
            && x.Protocol == y.Protocol
            && string.Equals(x.Source, y.Source, StringComparison.Ordinal)
            && string.Equals(x.SourcePorts, y.SourcePorts, StringComparison.Ordinal)
            && string.Equals(x.SourceInterface, y.SourceInterface, StringComparison.Ordinal)
            && string.Equals(x.Destination, y.Destination, StringComparison.Ordinal)
            && string.Equals(x.DestinationPorts, y.DestinationPorts, StringComparison.Ordinal)
            && string.Equals(x.DestinationInterface, y.DestinationInterface, StringComparison.Ordinal)
            && string.Equals(x.Comment, y.Comment, StringComparison.Ordinal);
    }

    private static string NormalizeRawLine(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return string.Empty;
        }

        int closingBracket = rawLine.IndexOf(']');
        return closingBracket >= 0 ? rawLine[(closingBracket + 1)..].Trim() : rawLine.Trim();
    }
}
