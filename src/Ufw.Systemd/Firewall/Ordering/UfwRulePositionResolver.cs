using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall.Ordering;

internal static class UfwRulePositionResolver
{
    public static int GetFamilyPosition(IReadOnlyList<ListedFirewallRule> rules, int occurrenceIndex)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (occurrenceIndex < 0 || occurrenceIndex >= rules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceIndex), occurrenceIndex, "Rule occurrence is outside the current snapshot.");
        }

        FirewallAddressFamily family = GetObservedFamily(rules[occurrenceIndex]);
        int position = 0;
        for (int index = 0; index <= occurrenceIndex; index++)
        {
            if (GetObservedFamily(rules[index]) == family)
            {
                position++;
            }
        }
        return position;
    }

    public static int CountFamily(IReadOnlyList<ListedFirewallRule> rules, FirewallAddressFamily family)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureConcreteFamily(family);
        return rules.Count(rule => GetObservedFamily(rule) == family);
    }

    public static int? FindNextFamilyOccurrence(
        IReadOnlyList<ListedFirewallRule> rules,
        int occurrenceIndex,
        FirewallAddressFamily family)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureConcreteFamily(family);
        for (int index = occurrenceIndex + 1; index < rules.Count; index++)
        {
            if (GetObservedFamily(rules[index]) == family)
            {
                return index;
            }
        }
        return null;
    }

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

    private static void EnsureConcreteFamily(FirewallAddressFamily family)
    {
        if (family is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new InvalidOperationException("UFW positional operations require a concrete IPv4 or IPv6 rule family.");
        }
    }
}
