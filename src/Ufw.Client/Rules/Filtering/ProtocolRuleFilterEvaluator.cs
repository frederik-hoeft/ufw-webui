using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering;

internal sealed class ProtocolRuleFilterEvaluator : RuleFilterEvaluator<ProtocolRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, ProtocolRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        FirewallRuleSpecification? rule = row.Rule.Rule;
        return row.Rule.Parsed && rule is not null && rule.Protocol == filter.Protocol
            ? RuleMatchEvaluation.Match(new FieldRuleMatchEvidence(FieldRuleMatchEvidence.FieldKind.Protocol, RuleSpecificationNormalizer.FormatProtocol(rule.Protocol)))
            : RuleMatchEvaluation.NoMatch;
    }
}
