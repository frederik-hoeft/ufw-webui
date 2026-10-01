using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Authoring form of one ordered rule before it is resolved against a <see cref="PolicyWorld{TAddress}"/>.
/// A null match component means the whole universe of that axis. An empty set matches nothing.
/// Interface components are legal only on chains that have that axis.
/// </summary>
public sealed class PolicyRuleDefinition<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
{
    /// <summary>Gets the opaque rule identity.</summary>
    public required RuleId Id { get; init; }

    /// <summary>Gets the chain the rule is evaluated on.</summary>
    public required TrafficChain Chain { get; init; }

    /// <summary>Gets the terminal decision. <see cref="PolicyDecision.Limit"/> stays conditional.</summary>
    public required PolicyDecision Decision { get; init; }

    /// <summary>Gets the source addresses, or <see langword="null"/> for every address.</summary>
    public IntervalSet<TAddress>? Source { get; init; }

    /// <summary>Gets the source ports, or <see langword="null"/> for no port restriction. A numeric port constraint excludes protocols without port semantics.</summary>
    public IntervalSet<ushort>? SourcePorts { get; init; }

    /// <summary>Gets the destination addresses, or <see langword="null"/> for every address.</summary>
    public IntervalSet<TAddress>? Destination { get; init; }

    /// <summary>Gets the destination ports, or <see langword="null"/> for no port restriction. A numeric port constraint excludes protocols without port semantics.</summary>
    public IntervalSet<ushort>? DestinationPorts { get; init; }

    /// <summary>Gets the protocols, or <see langword="null"/> for every protocol in the world.</summary>
    public FiniteSet<ProtocolSymbol>? Protocols { get; init; }

    /// <summary>Gets the ingress interfaces. Null means every known interface when the chain has ingress.</summary>
    public FiniteSet<NetworkInterfaceName>? Ingress { get; init; }

    /// <summary>Gets the egress interfaces. Null means every known interface when the chain has egress.</summary>
    public FiniteSet<NetworkInterfaceName>? Egress { get; init; }
}
