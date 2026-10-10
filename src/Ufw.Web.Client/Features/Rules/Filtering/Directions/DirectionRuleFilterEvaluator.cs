using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Directions;

internal sealed class DirectionRuleFilterEvaluator : EnumRuleFilterEvaluator<DirectionRuleFilter, FirewallDirection>
{
    public DirectionRuleFilterEvaluator() : base(
        static rule => rule.Direction,
        static filter => filter.Direction,
        static value => new DirectionRuleMatchEvidence(RuleSpecificationNormalizer.FormatDirection(value)))
    {
    }
}
