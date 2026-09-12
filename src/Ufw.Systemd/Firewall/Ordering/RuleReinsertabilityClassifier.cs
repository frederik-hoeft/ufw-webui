using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class RuleReinsertabilityClassifier(IUfwRuleCommandRenderer renderer) : IRuleReinsertabilityClassifier
{
    public RuleReinsertability Classify(ListedFirewallRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!rule.Parsed || rule.Rule is null)
        {
            return Unsupported("The authoritative UFW row could not be represented losslessly.");
        }

        FirewallRuleSpecification specification = RuleSpecificationNormalizer.Normalize(rule.Rule);
        if (specification.AddressFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            return Unsupported("Reordering requires a concrete address-family materialization.");
        }

        if (!renderer.TryRender(specification, out UfwRenderedRule? renderedRule))
        {
            return Unsupported("The authoritative rule cannot be rendered back to supported UFW syntax.");
        }

        return new RuleReinsertability(
            IsReinsertable: true,
            Reason: null,
            Specification: specification,
            RenderedRule: renderedRule,
            KeepPriority: renderedRule.Arguments.Length);
    }

    private static RuleReinsertability Unsupported(string reason) => new(false, reason, null, null, int.MaxValue);
}
