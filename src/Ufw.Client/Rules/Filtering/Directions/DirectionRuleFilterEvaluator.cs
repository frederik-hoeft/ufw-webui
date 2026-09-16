using Ufw.Client.Rules.Filtering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering.Directions;

internal sealed class DirectionRuleFilterEvaluator : RuleFilterEvaluator<DirectionRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, DirectionRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        FirewallRuleSpecification? rule = row.Rule.Rule;
        return row.Rule.Parsed && rule is not null && rule.Direction == filter.Direction
            ? RuleMatchEvaluation.Match(new DirectionRuleMatchEvidence(RuleSpecificationNormalizer.FormatDirection(rule.Direction)))
            : RuleMatchEvaluation.NoMatch;
    }
}
