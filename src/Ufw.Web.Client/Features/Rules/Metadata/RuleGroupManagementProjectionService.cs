using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed class RuleGroupManagementProjectionService(IRuleListProjectionService ruleProjection) : IRuleGroupManagementProjectionService
{
    public IReadOnlyList<RuleGroupManagementProjection> Create(IReadOnlyList<RuleGroup> groups, RuleSnapshot? ruleSnapshot)
    {
        ArgumentNullException.ThrowIfNull(groups);

        if (ruleSnapshot is null)
        {
            return groups.Select(static group => new RuleGroupManagementProjection(
                group,
                group.RuleIds.Select(static ruleId => new RuleGroupMemberProjection(ruleId, [])).ToArray(),
                MemberResolutionAvailable: false)).ToArray();
        }

        RuleListProjection projectedRules = ruleProjection.Create(ruleSnapshot.Rules, orderingPreview: null, ruleSnapshot.Metadata);
        Dictionary<string, IReadOnlyList<RuleRowProjection>> occurrencesByRuleId = projectedRules.Families
            .SelectMany(static family => family.Rows)
            .Where(static row => !string.IsNullOrWhiteSpace(row.Rule.RuleId))
            .GroupBy(static row => row.Rule.RuleId!, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<RuleRowProjection>)group
                    .OrderBy(static row => row.AddressFamily)
                    .ThenBy(static row => row.FamilyPosition)
                    .ToArray(),
                StringComparer.Ordinal);

        return groups.Select(group => new RuleGroupManagementProjection(
            group,
            group.RuleIds.Select(ruleId => new RuleGroupMemberProjection(ruleId, occurrencesByRuleId.GetValueOrDefault(ruleId) ?? [])).ToArray(),
            MemberResolutionAvailable: true)).ToArray();
    }
}
