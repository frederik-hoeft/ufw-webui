using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Groups;

internal sealed class GroupRuleFilterEvaluator : RuleFilterEvaluator<GroupRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, GroupRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        RuleGroupMembership? group = row.Metadata?.Group;
        return group?.Id == filter.Group.Id
            ? RuleMatchEvaluation.Match(new GroupRuleMatchEvidence(group))
            : RuleMatchEvaluation.NoMatch;
    }
}
