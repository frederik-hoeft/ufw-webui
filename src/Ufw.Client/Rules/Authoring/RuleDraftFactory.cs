using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Authoring;

internal sealed class RuleDraftFactory : IRuleDraftFactory
{
    public FirewallRuleSpecification Create() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.Any,
        Direction = FirewallDirection.Forward,
        Protocol = FirewallProtocol.Any,
    };
}
