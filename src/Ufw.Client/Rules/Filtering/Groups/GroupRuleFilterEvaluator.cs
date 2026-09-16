using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Groups;

internal sealed class GroupRuleFilterEvaluator : RuleFilterEvaluator<GroupRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, GroupRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        string? group = row.Metadata?.Group;
        return group is not null && string.Equals(group, filter.Group, StringComparison.OrdinalIgnoreCase)
            ? RuleMatchEvaluation.Match(new GroupRuleMatchEvidence(group))
            : RuleMatchEvaluation.NoMatch;
    }
}
