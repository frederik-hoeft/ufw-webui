using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Closed-world snapshot the evaluator reads. It is pure data: address family, finite universes, defaults, and ordered rules.
/// </summary>
public sealed class PolicyWorld<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
{
    private readonly Dictionary<ProtocolSymbol, ProtocolDefinition> _protocolsBySymbol;
    private readonly PolicyRule<TAddress>[] _rules;

    private PolicyWorld(
        IpFamily family,
        IntervalSet<TAddress> addressUniverse,
        FiniteSet<ProtocolDefinition> protocolDefinitions,
        FiniteSet<NetworkInterfaceName> interfaces,
        PolicyDecision incomingDefault,
        PolicyDecision outgoingDefault,
        PolicyDecision routedDefault,
        IReadOnlyList<PolicyRuleDefinition<TAddress>> definitions)
    {
        Family = family;
        AddressUniverse = addressUniverse;
        PortUniverse = PacketPorts.Universe;
        ProtocolDefinitions = protocolDefinitions;
        Protocols = FiniteSet<ProtocolSymbol>.From(protocolDefinitions.Values.Select(static definition => definition.Symbol));
        Interfaces = interfaces;
        IncomingDefault = incomingDefault;
        OutgoingDefault = outgoingDefault;
        RoutedDefault = routedDefault;
        _protocolsBySymbol = protocolDefinitions.Values.ToDictionary(static definition => definition.Symbol);
        _rules = ResolveRules(definitions);
    }

    /// <summary>Gets the concrete address family.</summary>
    public IpFamily Family { get; }

    /// <summary>Gets every address the family can name.</summary>
    public IntervalSet<TAddress> AddressUniverse { get; }

    /// <summary>Gets every numeric port available to protocols that use ports.</summary>
    public IntervalSet<ushort> PortUniverse { get; }

    /// <summary>Gets the protocol definitions in the closed world.</summary>
    public FiniteSet<ProtocolDefinition> ProtocolDefinitions { get; }

    /// <summary>Gets the protocol symbols <c>any</c> expands to.</summary>
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
        IReadOnlyCollection<ProtocolDefinition> protocols,
        FiniteSet<NetworkInterfaceName> interfaces,
        PolicyDecision incomingDefault,
        PolicyDecision outgoingDefault,
        PolicyDecision routedDefault,
        IReadOnlyList<PolicyRuleDefinition<TAddress>> rules)
    {
        ArgumentNullException.ThrowIfNull(protocols);
        ArgumentNullException.ThrowIfNull(rules);
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
        FiniteSet<ProtocolDefinition> protocolSet = NormalizeProtocols(protocols);
        ValidateInterfaces(interfaces);

        return new PolicyWorld<TAddress>(family, addressUniverse, protocolSet, interfaces, incomingDefault, outgoingDefault, routedDefault, rules);
    }

    internal bool ProtocolUsesPorts(ProtocolSymbol protocol)
    {
        if (!_protocolsBySymbol.TryGetValue(protocol, out ProtocolDefinition definition))
        {
            throw new ArgumentException("The packet protocol is outside the closed world.", nameof(protocol));
        }

        return definition.UsesPorts;
    }

    internal bool HasEquivalentPacketUniverse(PolicyWorld<TAddress> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Family == other.Family
            && AddressUniverse.Equals(other.AddressUniverse)
            && ProtocolDefinitions.Equals(other.ProtocolDefinitions)
            && Interfaces.Equals(other.Interfaces);
    }

    private PolicyRule<TAddress>[] ResolveRules(IReadOnlyList<PolicyRuleDefinition<TAddress>> definitions)
    {
        PolicyRule<TAddress>[] resolved = new PolicyRule<TAddress>[definitions.Count];
        for (int index = 0; index < definitions.Count; index++)
        {
            PolicyRuleDefinition<TAddress> definition = definitions[index] ?? throw new ArgumentException("Rules cannot contain null entries.", nameof(definitions));
            resolved[index] = Resolve(definition, index);
        }

        return resolved;
    }

    private PolicyRule<TAddress> Resolve(PolicyRuleDefinition<TAddress> definition, int familyOrder)
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

        PolicyConstraint<TAddress> constraint = new()
        {
            Source = definition.Source,
            SourcePorts = definition.SourcePorts,
            Destination = definition.Destination,
            DestinationPorts = definition.DestinationPorts,
            Protocols = definition.Protocols,
            Ingress = definition.Ingress,
            Egress = definition.Egress,
        };
        PacketLayout<TAddress> layout = PacketLayout<TAddress>.Create(this, ChainProfile.For(definition.Chain));
        PacketSpace<TAddress> match = new(layout, layout.Materialize(constraint));
        return new PolicyRule<TAddress>(definition.Id, familyOrder, definition.Chain, definition.Decision, match);
    }

    private static FiniteSet<ProtocolDefinition> NormalizeProtocols(IReadOnlyCollection<ProtocolDefinition> protocols)
    {
        if (protocols.Count == 0)
        {
            throw new ArgumentException("The protocol universe must name at least one protocol.", nameof(protocols));
        }

        FiniteSet<ProtocolDefinition> result = FiniteSet<ProtocolDefinition>.From(protocols);
        HashSet<ProtocolSymbol> symbols = [];
        foreach (ProtocolDefinition definition in result.Values)
        {
            if (string.IsNullOrEmpty(definition.Symbol.Name))
            {
                throw new ArgumentException("The protocol universe contains an empty protocol.", nameof(protocols));
            }

            if (!symbols.Add(definition.Symbol))
            {
                throw new ArgumentException($"Protocol '{definition.Symbol}' is defined more than once.", nameof(protocols));
            }
        }

        return result;
    }

    private static void ValidateInterfaces(FiniteSet<NetworkInterfaceName> interfaces)
    {
        foreach (NetworkInterfaceName name in interfaces.Values)
        {
            if (string.IsNullOrEmpty(name.Name))
            {
                throw new ArgumentException("The interface universe contains an empty interface name.", nameof(interfaces));
            }
        }
    }

    private static void ValidateDefault(PolicyDecision decision, string name)
    {
        if (decision is not (PolicyDecision.Allow or PolicyDecision.Deny or PolicyDecision.Reject))
        {
            throw new ArgumentOutOfRangeException(name, decision, "Default policy must be allow, deny, or reject.");
        }
    }
}
