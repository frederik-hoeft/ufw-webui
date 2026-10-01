using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Factories for a closed policy world. The address universe is the entire family, not the addresses named by rules.
/// </summary>
public static class PolicyWorld
{
    /// <summary>Creates an IPv4 world. <paramref name="rules"/> is first-match order across every chain.</summary>
    public static PolicyWorld<uint> CreateIPv4(
        IReadOnlyCollection<ProtocolDefinition> protocols,
        FiniteSet<NetworkInterfaceName> interfaces,
        PolicyDecision incomingDefault,
        PolicyDecision outgoingDefault,
        PolicyDecision routedDefault,
        IReadOnlyList<PolicyRuleDefinition<uint>> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return PolicyWorld<uint>.Create(
            IpFamily.IPv4,
            NetworkAddress.IPv4Universe,
            protocols,
            interfaces,
            incomingDefault,
            outgoingDefault,
            routedDefault,
            rules);
    }

    /// <summary>Creates an IPv6 world. <paramref name="rules"/> is first-match order across every chain.</summary>
    public static PolicyWorld<UInt128> CreateIPv6(
        IReadOnlyCollection<ProtocolDefinition> protocols,
        FiniteSet<NetworkInterfaceName> interfaces,
        PolicyDecision incomingDefault,
        PolicyDecision outgoingDefault,
        PolicyDecision routedDefault,
        IReadOnlyList<PolicyRuleDefinition<UInt128>> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return PolicyWorld<UInt128>.Create(
            IpFamily.IPv6,
            NetworkAddress.IPv6Universe,
            protocols,
            interfaces,
            incomingDefault,
            outgoingDefault,
            routedDefault,
            rules);
    }
}
