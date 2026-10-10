using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal abstract class EnumRuleFilterEvaluator<TFilter, TValue>(
    Func<FirewallRuleSpecification, TValue> getRuleValue,
    Func<TFilter, TValue> getFilterValue,
    Func<TValue, RuleMatchEvidence> createEvidence) : RuleFilterEvaluator<TFilter>
    where TFilter : RuleFilter
    where TValue : struct, Enum
{
    protected sealed override RuleMatchEvaluation Evaluate(RuleRowProjection row, TFilter filter, RuleFilterContext context)
    {
        _ = context;
        FirewallRuleSpecification? rule = row.Rule.Rule;
        if (!row.Rule.Parsed || rule is null)
        {
            return RuleMatchEvaluation.NoMatch;
        }

        TValue ruleValue = getRuleValue(rule);
        return EqualityComparer<TValue>.Default.Equals(ruleValue, getFilterValue(filter))
            ? RuleMatchEvaluation.Match(createEvidence(ruleValue))
            : RuleMatchEvaluation.NoMatch;
    }
}
