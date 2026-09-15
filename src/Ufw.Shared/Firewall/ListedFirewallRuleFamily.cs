namespace Ufw.Shared.Firewall;

/// <summary>
/// Resolves the concrete address-family partition in which UFW displayed a listed rule.
/// </summary>
public static class ListedFirewallRuleFamily
{
    public static FirewallAddressFamily GetObservedFamily(ListedFirewallRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.Rule?.AddressFamily is FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6)
        {
            return rule.Rule.AddressFamily;
        }

        if (string.IsNullOrWhiteSpace(rule.RawLine))
        {
            throw new InvalidOperationException("The observed UFW rule does not expose an address family.");
        }

        return rule.RawLine.Contains("(v6)", StringComparison.Ordinal)
            ? FirewallAddressFamily.IPv6
            : FirewallAddressFamily.IPv4;
    }
}
