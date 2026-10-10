using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Actions;

internal sealed class ActionRuleFilterEvaluator : EnumRuleFilterEvaluator<ActionRuleFilter, FirewallAction>
{
    public ActionRuleFilterEvaluator() : base(
        static rule => rule.Action,
        static filter => filter.Action,
        static value => new ActionRuleMatchEvidence(RuleSpecificationNormalizer.FormatAction(value)))
    {
    }
}
