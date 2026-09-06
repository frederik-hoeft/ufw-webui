using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Client.Components.Rules;

internal static class FirewallRuleDefaults
{
    public static FirewallRuleSpecification Create() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.Any,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Any,
        Source = RuleSpecificationNormalizer.ANY,
        Destination = RuleSpecificationNormalizer.ANY,
    };
}
