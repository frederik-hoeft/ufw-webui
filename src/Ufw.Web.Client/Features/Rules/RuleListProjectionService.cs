using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;

namespace Ufw.Web.Client.Features.Rules;

internal sealed class RuleListProjectionService(IUfwRuleCommandRenderer commandRenderer) : IRuleListProjectionService
{
    public RuleListProjection Create(
        IReadOnlyList<ListedFirewallRule> rules,
        RuleOrderingPreview? orderingPreview,
        IReadOnlyDictionary<string, RuleMetadata>? metadataByRuleId = null)
    {
        ArgumentNullException.ThrowIfNull(rules);

        RuleSnapshotIndex index = new(rules);

        IReadOnlyList<int> projectedOrder = GetProjectedOrder(rules.Count, orderingPreview);
        Dictionary<FirewallAddressFamily, int> familyPositions = [];
        List<RuleRowProjection> ipv4Rows = [];
        List<RuleRowProjection> ipv6Rows = [];
        for (int projectedIndex = 0; projectedIndex < projectedOrder.Count; projectedIndex++)
        {
            int occurrenceId = projectedOrder[projectedIndex];
            ListedFirewallRule rule = rules[occurrenceId];
            FirewallAddressFamily family = index.GetFamily(occurrenceId);
            int familyPosition = familyPositions.GetValueOrDefault(family) + 1;
            familyPositions[family] = familyPosition;

            RulePositionChange? positionChange = CreatePositionChange(occurrenceId, index.GetFamilyPosition(occurrenceId), familyPosition, orderingPreview);
            bool canOrder = rule.Parsed && rule.Rule is not null;
            bool hasUniqueSemanticIdentity = canOrder
                && rule.RuleId is { } ruleId
                && index.GetIdentityMultiplicity(ruleId) == 1;
            bool canMutate = hasUniqueSemanticIdentity;
            bool canEdit = hasUniqueSemanticIdentity && rule.Rule!.AddressFamily is FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6;

            RuleMetadata? metadata = rule.RuleId is { } ruleIdentity
                && metadataByRuleId is not null
                && metadataByRuleId.TryGetValue(ruleIdentity, out RuleMetadata? matchedMetadata)
                    ? matchedMetadata
                    : null;
            RuleRowProjection row = new(rule, family, occurrenceId, familyPosition, index.GetFamilyCount(family), canOrder, canMutate, positionChange, metadata, CreateCanonicalCommand(rule))
            {
                CanEdit = canEdit,
                CanSaveAsTemplate = canOrder,
            };

            if (family == FirewallAddressFamily.IPv6)
            {
                ipv6Rows.Add(row);
            }
            else
            {
                ipv4Rows.Add(row);
            }
        }

        return new RuleListProjection([new RuleFamilyProjection(FirewallAddressFamily.IPv4, ipv4Rows), new RuleFamilyProjection(FirewallAddressFamily.IPv6, ipv6Rows),]);
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

        return new RulePositionChange(originalFamilyPosition, currentFamilyPosition, orderingPreview.WasDirectlyMoved(occurrenceId));
    }
}
