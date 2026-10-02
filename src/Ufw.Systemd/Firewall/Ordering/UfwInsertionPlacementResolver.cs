using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Firewall.Ordering;

internal static class UfwInsertionPlacementResolver
{
    public static UfwInsertionPlacement Resolve(IReadOnlyList<ListedFirewallRule> rules, FirewallAddressFamily family, int desiredOccurrenceIndex)
    {
        ArgumentNullException.ThrowIfNull(rules);
        FamilyPartition partition = GetPartition(rules, family);
        if (desiredOccurrenceIndex < partition.StartIndex || desiredOccurrenceIndex > partition.EndIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(desiredOccurrenceIndex),
                desiredOccurrenceIndex,
                $"Insertion occurrence {desiredOccurrenceIndex} is outside the {family} rule partition [{partition.StartIndex}, {partition.EndIndex}].");
        }

        int? ufwInsertPosition = desiredOccurrenceIndex < partition.EndIndex
            ? desiredOccurrenceIndex + 1
            : null;
        return new UfwInsertionPlacement(desiredOccurrenceIndex, ufwInsertPosition);
    }

    public static UfwInsertionPlacement ResolveFamilyPosition(IReadOnlyList<ListedFirewallRule> rules, FirewallAddressFamily family, int familyPosition)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (familyPosition <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(familyPosition), familyPosition, "Family positions are one-based and must be positive.");
        }

        FamilyPartition partition = GetPartition(rules, family);
        int familyOffset = Math.Min(familyPosition - 1, partition.Count);
        return Resolve(rules, family, partition.StartIndex + familyOffset);
    }

    public static int GetFamilyPosition(IReadOnlyList<ListedFirewallRule> rules, int occurrenceIndex)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (occurrenceIndex < 0 || occurrenceIndex >= rules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceIndex), occurrenceIndex, "Rule occurrence is outside the current snapshot.");
        }

        FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rules[occurrenceIndex]);
        FamilyPartition partition = GetPartition(rules, family);
        return occurrenceIndex - partition.StartIndex + 1;
    }

    private static FamilyPartition GetPartition(IReadOnlyList<ListedFirewallRule> rules, FirewallAddressFamily family)
    {
        EnsureConcreteFamily(family);

        int ipv4Count = 0;
        bool ipv6Seen = false;
        foreach (ListedFirewallRule rule in rules)
        {
            FirewallAddressFamily observedFamily = ListedFirewallRuleFamily.GetObservedFamily(rule);
            if (observedFamily == FirewallAddressFamily.IPv6)
            {
                ipv6Seen = true;
                continue;
            }

            if (ipv6Seen)
            {
                throw new InvalidOperationException("Authoritative UFW rule state must contain a contiguous IPv4 partition followed by a contiguous IPv6 partition.");
            }

            ipv4Count++;
        }

        return family == FirewallAddressFamily.IPv4
            ? new FamilyPartition(0, ipv4Count)
            : new FamilyPartition(ipv4Count, rules.Count);
    }

    private static void EnsureConcreteFamily(FirewallAddressFamily family)
    {
        if (family is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new InvalidOperationException("UFW positional operations require a concrete IPv4 or IPv6 rule family.");
        }
    }

    private readonly record struct FamilyPartition(int StartIndex, int EndIndex)
    {
        public int Count => EndIndex - StartIndex;
    }
}

internal readonly record struct UfwInsertionPlacement(int ExpectedOccurrenceIndex, int? UfwInsertPosition)
{
    public IUfwCommand CreateCommand(FirewallRuleSpecification specification, IUfwRuleCommandRenderer renderer) =>
        UfwInsertPosition is int position
            ? new UfwInsertRuleCommand(position, specification, renderer)
            : new UfwAddRuleCommand(specification, renderer);
}
