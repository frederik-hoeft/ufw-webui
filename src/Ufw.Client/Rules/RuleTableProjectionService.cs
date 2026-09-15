using Ufw.Client.RuleOrdering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules;

internal sealed class RuleTableProjectionService : IRuleTableProjectionService
{
    public RuleTableProjection Create(IReadOnlyList<ListedFirewallRule> rules, RuleOrderingPreview? orderingPreview)
    {
        ArgumentNullException.ThrowIfNull(rules);

        Dictionary<string, int> ruleIdCounts = new(StringComparer.Ordinal);
        Dictionary<FirewallAddressFamily, int> familyCounts = new();
        Dictionary<int, int> originalFamilyPositions = new();
        for (int occurrenceId = 0; occurrenceId < rules.Count; occurrenceId++)
        {
            ListedFirewallRule rule = rules[occurrenceId];
            if (!string.IsNullOrWhiteSpace(rule.RuleId))
            {
                ruleIdCounts[rule.RuleId] = ruleIdCounts.GetValueOrDefault(rule.RuleId) + 1;
            }

            FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rule);
            int familyPosition = familyCounts.GetValueOrDefault(family) + 1;
            familyCounts[family] = familyPosition;
            originalFamilyPositions[occurrenceId] = familyPosition;
        }

        IReadOnlyList<int> projectedOrder = GetProjectedOrder(rules.Count, orderingPreview);
        Dictionary<FirewallAddressFamily, int> familyPositions = new();
        List<RuleRowProjection> ipv4Rows = [];
        List<RuleRowProjection> ipv6Rows = [];
        for (int projectedIndex = 0; projectedIndex < projectedOrder.Count; projectedIndex++)
        {
            int occurrenceId = projectedOrder[projectedIndex];
            ListedFirewallRule rule = rules[occurrenceId];
            FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rule);
            int familyPosition = familyPositions.GetValueOrDefault(family) + 1;
            familyPositions[family] = familyPosition;

            RulePositionChange? positionChange = CreatePositionChange(occurrenceId, originalFamilyPositions[occurrenceId], familyPosition, orderingPreview);
            bool canOrder = rule.Parsed && rule.Rule is not null;
            bool canMutate = canOrder
                && rule.RuleId is { } ruleId
                && ruleIdCounts.GetValueOrDefault(ruleId) == 1;

            RuleRowProjection row = new(
                rule,
                family,
                occurrenceId,
                familyPosition,
                familyCounts[family],
                canOrder,
                canMutate,
                positionChange);

            if (family == FirewallAddressFamily.IPv6)
            {
                ipv6Rows.Add(row);
            }
            else
            {
                ipv4Rows.Add(row);
            }
        }

        List<RuleFamilyProjection> families = [];
        if (ipv4Rows.Count > 0)
        {
            families.Add(new RuleFamilyProjection(FirewallAddressFamily.IPv4, ipv4Rows));
        }
        if (ipv6Rows.Count > 0)
        {
            families.Add(new RuleFamilyProjection(FirewallAddressFamily.IPv6, ipv6Rows));
        }

        return new RuleTableProjection(families);
    }

    private static IReadOnlyList<int> GetProjectedOrder(int ruleCount, RuleOrderingPreview? orderingPreview)
    {
        if (orderingPreview is null)
        {
            return Enumerable.Range(0, ruleCount).ToArray();
        }
        if (orderingPreview.DesiredOrder.Count != ruleCount)
        {
            throw new InvalidOperationException("The ordering preview does not match the authoritative rule snapshot.");
        }

        return orderingPreview.DesiredOrder;
    }

    private static RulePositionChange? CreatePositionChange(
        int occurrenceId,
        int originalFamilyPosition,
        int currentFamilyPosition,
        RuleOrderingPreview? orderingPreview)
    {
        if (orderingPreview is null || originalFamilyPosition == currentFamilyPosition)
        {
            return null;
        }

        return new RulePositionChange(
            originalFamilyPosition,
            currentFamilyPosition,
            orderingPreview.WasDirectlyMoved(occurrenceId));
    }
}
