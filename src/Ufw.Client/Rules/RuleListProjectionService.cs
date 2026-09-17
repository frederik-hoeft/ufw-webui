using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules.Metadata;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Client.Rules;

internal sealed class RuleListProjectionService(IUfwRuleCommandRenderer commandRenderer) : IRuleListProjectionService
{
    public RuleListProjection Create(
        IReadOnlyList<ListedFirewallRule> rules,
        RuleOrderingPreview? orderingPreview,
        IReadOnlyDictionary<string, RuleMetadata>? metadataByRuleId = null)
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

            RuleMetadata? metadata = rule.RuleId is { } ruleIdentity
                && metadataByRuleId is not null
                && metadataByRuleId.TryGetValue(ruleIdentity, out RuleMetadata? matchedMetadata)
                    ? matchedMetadata
                    : null;
            RuleRowProjection row = new(
                rule,
                family,
                occurrenceId,
                familyPosition,
                familyCounts[family],
                canOrder,
                canMutate,
                positionChange,
                metadata,
                CreateCanonicalCommand(rule));

            if (family == FirewallAddressFamily.IPv6)
            {
                ipv6Rows.Add(row);
            }
            else
            {
                ipv4Rows.Add(row);
            }
        }

        return new RuleListProjection(
        [
            new RuleFamilyProjection(FirewallAddressFamily.IPv4, ipv4Rows),
            new RuleFamilyProjection(FirewallAddressFamily.IPv6, ipv6Rows),
        ]);
    }

    private string? CreateCanonicalCommand(ListedFirewallRule rule)
    {
        if (!rule.Parsed || rule.Rule is null || !commandRenderer.TryRender(rule.Rule, out UfwRenderedRule? rendered))
        {
            return null;
        }

        return rendered.DisplayText;
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
