using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

internal static class FirewallRuleDefaults
{
    public static FirewallRuleSpecification Create() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.Any,
        Direction = FirewallDirection.Forward,
        Protocol = FirewallProtocol.Any
    };
}
