using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Mock.State;

namespace Ufw.Mock.Rules;

internal static class UfwRuleComparer
{
    public static bool SemanticallyEqual(UfwMockRule left, UfwMockRule right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return RuleIdentity.AreEqual(left.Specification, right.Specification)
            && HasEqualMockSurface(left, right);
    }

    public static bool SemanticallyEqualIgnoringAddressFamily(UfwMockRule left, UfwMockRule right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        FirewallRuleSpecification leftForRightFamily = CloneWithAddressFamily(
            left.Specification,
            right.Specification.AddressFamily);
        return RuleIdentity.AreEqual(leftForRightFamily, right.Specification)
            && HasEqualMockSurface(left, right);
    }

    private static bool HasEqualMockSurface(UfwMockRule left, UfwMockRule right) =>
        string.Equals(left.ExtendedProtocol, right.ExtendedProtocol, StringComparison.OrdinalIgnoreCase)
        && left.Logging == right.Logging
        && string.Equals(left.SourceApplicationName, right.SourceApplicationName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.DestinationApplicationName, right.DestinationApplicationName, StringComparison.OrdinalIgnoreCase);

    private static FirewallRuleSpecification CloneWithAddressFamily(
        FirewallRuleSpecification specification,
        FirewallAddressFamily addressFamily) => new()
        {
            Action = specification.Action,
            AddressFamily = addressFamily,
            Direction = specification.Direction,
            Protocol = specification.Protocol,
            Source = specification.Source,
            SourcePorts = specification.SourcePorts,
            SourceInterface = specification.SourceInterface,
            Destination = specification.Destination,
            DestinationPorts = specification.DestinationPorts,
            DestinationInterface = specification.DestinationInterface,
            Comment = specification.Comment,
        };
}
