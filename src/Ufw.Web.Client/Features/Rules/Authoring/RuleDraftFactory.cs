using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed class RuleDraftFactory : IRuleDraftFactory
{
    public FirewallRuleSpecification Create() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.Any,
        Direction = FirewallDirection.Forward,
        Protocol = FirewallProtocol.Any,
    };

    public FirewallRuleSpecification CreateFromExisting(FirewallRuleSpecification rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return RuleSpecificationNormalizer.Normalize(rule);
    }
}
