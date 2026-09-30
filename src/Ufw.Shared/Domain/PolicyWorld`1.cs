using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Closed-world snapshot the evaluator reads. It is pure data: address family, universes, defaults, and ordered rules.
/// </summary>
/// <remarks>
/// The address universe is every address of the family. The port universe is <see cref="PacketPorts.Universe"/>.
/// Protocols and interfaces are exactly the finite sets given at construction. A later element is a new world, not a new evaluator.
/// </remarks>
public sealed class PolicyWorld<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
{
    private readonly PolicyRule<TAddress>[] _rules;

    private PolicyWorld(
        IpFamily family,
        IntervalSet<TAddress> addressUniverse,
        FiniteSet<ProtocolSymbol> protocols,
        FiniteSet<NetworkInterfaceName> interfaces,
        PolicyDecision incomingDefault,
        PolicyDecision outgoingDefault,
        PolicyDecision routedDefault,
        PolicyRule<TAddress>[] rules)
    {
        Family = family;
        AddressUniverse = addressUniverse;
        PortUniverse = PacketPorts.Universe;
        Protocols = protocols;
        Interfaces = interfaces;
        IncomingDefault = incomingDefault;
        OutgoingDefault = outgoingDefault;
        RoutedDefault = routedDefault;
        _rules = rules;
    }

    /// <summary>Gets the concrete address family.</summary>
    public IpFamily Family { get; }

    /// <summary>Gets every address the family can name.</summary>
    public IntervalSet<TAddress> AddressUniverse { get; }

    /// <summary>Gets every modeled port.</summary>
    public IntervalSet<ushort> PortUniverse { get; }

    /// <summary>Gets the protocols <c>any</c> expands to.</summary>
    public FiniteSet<ProtocolSymbol> Protocols { get; }

    /// <summary>Gets the interfaces <c>any</c> expands to.</summary>
    public FiniteSet<NetworkInterfaceName> Interfaces { get; }

    /// <summary>Gets the default decision for <see cref="TrafficChain.Input"/>.</summary>
    public PolicyDecision IncomingDefault { get; }

    /// <summary>Gets the default decision for <see cref="TrafficChain.Output"/>.</summary>
    public PolicyDecision OutgoingDefault { get; }

    /// <summary>Gets the default decision for <see cref="TrafficChain.Forward"/>.</summary>
    public PolicyDecision RoutedDefault { get; }

    /// <summary>Gets the rules in first-match order. The list includes every chain.</summary>
    public IReadOnlyList<PolicyRule<TAddress>> Rules => _rules;

    /// <summary>Returns the default decision for <paramref name="chain"/>.</summary>
    public PolicyDecision DefaultFor(TrafficChain chain) => chain switch
    {
        TrafficChain.Input => IncomingDefault,
        TrafficChain.Output => OutgoingDefault,
        TrafficChain.Forward => RoutedDefault,
        _ => throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unsupported traffic chain."),
    };

    internal static PolicyWorld<TAddress> Create(
        IpFamily family,
        IntervalSet<TAddress> addressUniverse,
        FiniteSet<ProtocolSymbol> protocols,
        FiniteSet<NetworkInterfaceName> interfaces,
        PolicyDecision incomingDefault,
        PolicyDecision outgoingDefault,
        PolicyDecision routedDefault,
        IReadOnlyList<PolicyRuleDefinition<TAddress>> rules)
    {
        if (family == IpFamily.IPv4 && typeof(TAddress) != typeof(uint)
            || family == IpFamily.IPv6 && typeof(TAddress) != typeof(UInt128))
        {
            throw new ArgumentException("The address family does not match the address integer width.", nameof(family));
        }

        if (!Enum.IsDefined(family))
        {
            throw new ArgumentOutOfRangeException(nameof(family), family, "Unsupported address family.");
        }

        ValidateDefault(incomingDefault, nameof(incomingDefault));
        ValidateDefault(outgoingDefault, nameof(outgoingDefault));
        ValidateDefault(routedDefault, nameof(routedDefault));
        ValidateSymbols(protocols, interfaces);

        PolicyRule<TAddress>[] resolved = new PolicyRule<TAddress>[rules.Count];
        for (int index = 0; index < rules.Count; index++)
        {
            PolicyRuleDefinition<TAddress> definition = rules[index] ?? throw new ArgumentException("Rules cannot contain null entries.", nameof(rules));
            resolved[index] = Resolve(definition, index, addressUniverse, protocols, interfaces);
        }

        return new PolicyWorld<TAddress>(
            family,
            addressUniverse,
            protocols,
            interfaces,
            incomingDefault,
            outgoingDefault,
            routedDefault,
            resolved);
    }

    private static PolicyRule<TAddress> Resolve(
        PolicyRuleDefinition<TAddress> definition,
        int familyOrder,
        IntervalSet<TAddress> addressUniverse,
        FiniteSet<ProtocolSymbol> protocols,
        FiniteSet<NetworkInterfaceName> interfaces)
    {
        if (string.IsNullOrEmpty(definition.Id.Value))
        {
            throw new ArgumentException("A rule identity is required.", nameof(definition));
        }

        if (!Enum.IsDefined(definition.Chain))
        {
            throw new ArgumentOutOfRangeException(nameof(definition), definition.Chain, "Unsupported traffic chain.");
        }

        if (!Enum.IsDefined(definition.Decision))
        {
            throw new ArgumentOutOfRangeException(nameof(definition), definition.Decision, "Unsupported policy decision.");
        }

        ChainProfile profile = ChainProfile.For(definition.Chain);
        IntervalSet<TAddress> source = ResolveInterval(definition.Source, addressUniverse, "source address");
        IntervalSet<ushort> sourcePorts = ResolveInterval(definition.SourcePorts, PacketPorts.Universe, "source port");
        IntervalSet<TAddress> destination = ResolveInterval(definition.Destination, addressUniverse, "destination address");
        IntervalSet<ushort> destinationPorts = ResolveInterval(definition.DestinationPorts, PacketPorts.Universe, "destination port");
        FiniteSet<ProtocolSymbol> ruleProtocols = ResolveFinite(definition.Protocols, protocols, "protocol");
        FiniteSet<NetworkInterfaceName>? ingress = ResolveInterface(profile.HasIngress, definition.Ingress, interfaces, "ingress", definition.Id);
        FiniteSet<NetworkInterfaceName>? egress = ResolveInterface(profile.HasEgress, definition.Egress, interfaces, "egress", definition.Id);
        PacketRegion<TAddress> match = new(source, sourcePorts, destination, destinationPorts, ruleProtocols, ingress, egress);
        return new PolicyRule<TAddress>(definition.Id, familyOrder, definition.Chain, definition.Decision, match);
    }

    private static IntervalSet<T> ResolveInterval<T>(IntervalSet<T>? specified, IntervalSet<T> universe, string axis)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        if (specified is null)
        {
            return universe;
        }

        if (!universe.IsSupersetOf(specified.Value))
        {
            throw new ArgumentException($"The {axis} match is outside the closed world.");
        }

        return specified.Value;
    }

    private static FiniteSet<TSymbol> ResolveFinite<TSymbol>(FiniteSet<TSymbol>? specified, FiniteSet<TSymbol> universe, string axis)
        where TSymbol : IEquatable<TSymbol>, IComparable<TSymbol>
    {
        if (specified is null)
        {
            return universe;
        }

        if (!universe.IsSupersetOf(specified.Value))
        {
            throw new ArgumentException($"The {axis} match is outside the closed world.");
        }

        return specified.Value;
    }

    private static FiniteSet<NetworkInterfaceName>? ResolveInterface(
        bool axisExists,
        FiniteSet<NetworkInterfaceName>? specified,
        FiniteSet<NetworkInterfaceName> universe,
        string axis,
        RuleId id)
    {
        if (!axisExists)
        {
            if (specified is not null)
            {
                throw new ArgumentException($"Rule '{id}' cannot constrain {axis} on this chain.");
            }

            return null;
        }

        if (specified is null)
        {
            return universe;
        }

        if (!universe.IsSupersetOf(specified.Value))
        {
            throw new ArgumentException($"Rule '{id}' names a {axis} interface that is outside the closed world.");
        }

        return specified.Value;
    }

    private static void ValidateDefault(PolicyDecision decision, string name)
    {
        if (decision is not (PolicyDecision.Allow or PolicyDecision.Deny or PolicyDecision.Reject))
        {
            throw new ArgumentOutOfRangeException(name, decision, "Default policy must be allow, deny, or reject.");
        }
    }

    private static void ValidateSymbols(FiniteSet<ProtocolSymbol> protocols, FiniteSet<NetworkInterfaceName> interfaces)
    {
        if (protocols.IsEmpty)
        {
            throw new ArgumentException("The protocol universe must name at least one protocol.", nameof(protocols));
        }

        foreach (ProtocolSymbol protocol in protocols.Values)
        {
            if (string.IsNullOrEmpty(protocol.Name))
            {
                throw new ArgumentException("The protocol universe contains an empty protocol.", nameof(protocols));
            }
        }

        foreach (NetworkInterfaceName name in interfaces.Values)
        {
            if (string.IsNullOrEmpty(name.Name))
            {
                throw new ArgumentException("The interface universe contains an empty interface name.", nameof(interfaces));
            }
        }
    }
}
