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
        foreach (ListedFirewallRule rule in rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.RuleId))
            {
                ruleIdCounts[rule.RuleId] = ruleIdCounts.GetValueOrDefault(rule.RuleId) + 1;
            }

            FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rule);
            familyCounts[family] = familyCounts.GetValueOrDefault(family) + 1;
        }

        Dictionary<FirewallAddressFamily, int> familyPositions = new();
        List<RuleRowProjection> ipv4Rows = [];
        List<RuleRowProjection> ipv6Rows = [];
        for (int index = 0; index < rules.Count; index++)
        {
            ListedFirewallRule rule = rules[index];
            FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rule);
            int familyPosition = familyPositions.GetValueOrDefault(family) + 1;
            familyPositions[family] = familyPosition;

            int occurrenceId = orderingPreview?.GetOccurrenceId(rule) ?? index;
            RulePositionChange? positionChange = CreatePositionChange(rule, orderingPreview);
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

    private static RulePositionChange? CreatePositionChange(ListedFirewallRule rule, RuleOrderingPreview? orderingPreview)
    {
        int? originalPosition = orderingPreview?.GetOriginalPosition(rule);
        if (originalPosition is null || rule.DisplayNumber is null || originalPosition.Value == rule.DisplayNumber.Value)
        {
            return null;
        }

        return new RulePositionChange(
            originalPosition.Value,
            rule.DisplayNumber.Value,
            orderingPreview!.WasDirectlyMoved(rule));
    }
}
