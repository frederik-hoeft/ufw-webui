using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Protocols;

internal sealed class ProtocolRuleFilterEvaluator : EnumRuleFilterEvaluator<ProtocolRuleFilter, FirewallProtocol>
{
    public ProtocolRuleFilterEvaluator() : base(
        static rule => rule.Protocol,
        static filter => filter.Protocol,
        static value => new ProtocolRuleMatchEvidence(RuleSpecificationNormalizer.FormatProtocol(value)))
    {
    }
}
