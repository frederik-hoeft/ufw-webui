using Ufw.Client.Rules.Filtering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering.Actions;

internal sealed class ActionRuleFilterEvaluator : RuleFilterEvaluator<ActionRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, ActionRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        FirewallRuleSpecification? rule = row.Rule.Rule;
        return row.Rule.Parsed && rule is not null && rule.Action == filter.Action
            ? RuleMatchEvaluation.Match(new ActionRuleMatchEvidence(RuleSpecificationNormalizer.FormatAction(rule.Action)))
            : RuleMatchEvaluation.NoMatch;
    }
}
