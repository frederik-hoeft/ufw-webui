using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Protocols;

internal sealed class ProtocolRuleFilterEvaluator : RuleFilterEvaluator<ProtocolRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, ProtocolRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        FirewallRuleSpecification? rule = row.Rule.Rule;
        return row.Rule.Parsed && rule is not null && rule.Protocol == filter.Protocol
            ? RuleMatchEvaluation.Match(new ProtocolRuleMatchEvidence(RuleSpecificationNormalizer.FormatProtocol(rule.Protocol)))
            : RuleMatchEvaluation.NoMatch;
    }
}
