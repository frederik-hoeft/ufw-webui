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

        FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rules[occurrenceIndex]);
        int position = 0;
        for (int index = 0; index <= occurrenceIndex; index++)
        {
            if (ListedFirewallRuleFamily.GetObservedFamily(rules[index]) == family)
            {
                position++;
            }
        }
        return position;
    }

    public static int GetUfwInsertPosition(IReadOnlyList<ListedFirewallRule> rules, int occurrenceIndex)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (occurrenceIndex < 0 || occurrenceIndex >= rules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceIndex), occurrenceIndex, "Rule occurrence is outside the current snapshot.");
        }

        FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rules[occurrenceIndex]);
        int familyPosition = GetFamilyPosition(rules, occurrenceIndex);
        return GetUfwInsertPosition(rules, family, familyPosition);
    }

    public static int GetUfwInsertPosition(IReadOnlyList<ListedFirewallRule> rules, FirewallAddressFamily family, int familyPosition)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureConcreteFamily(family);
        int familyCount = CountFamily(rules, family);
        if (familyPosition <= 0 || familyPosition > familyCount)
        {
            throw new ArgumentOutOfRangeException(nameof(familyPosition), familyPosition, "UFW insert positions must identify an existing rule in the target family.");
        }

        return family == FirewallAddressFamily.IPv6
            ? CountFamily(rules, FirewallAddressFamily.IPv4) + familyPosition
            : familyPosition;
    }

    public static int CountFamily(IReadOnlyList<ListedFirewallRule> rules, FirewallAddressFamily family)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureConcreteFamily(family);
        return rules.Count(rule => ListedFirewallRuleFamily.GetObservedFamily(rule) == family);
    }

    public static int? FindNextFamilyOccurrence(IReadOnlyList<ListedFirewallRule> rules, int occurrenceIndex, FirewallAddressFamily family)
    {
        ArgumentNullException.ThrowIfNull(rules);
        EnsureConcreteFamily(family);
        for (int index = occurrenceIndex + 1; index < rules.Count; index++)
        {
            if (ListedFirewallRuleFamily.GetObservedFamily(rules[index]) == family)
            {
                return index;
            }
        }
        return null;
    }

    private static void EnsureConcreteFamily(FirewallAddressFamily family)
    {
        if (family is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new InvalidOperationException("UFW positional operations require a concrete IPv4 or IPv6 rule family.");
        }
    }
}
