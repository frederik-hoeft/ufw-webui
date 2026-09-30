using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Optional restriction of a packet space. A null component means that axis is not restricted.
/// An empty set restricts the axis to nothing, so the resulting space is empty.
/// </summary>
public sealed class PolicyConstraint<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
{
    /// <summary>Gets a constraint that leaves every axis open.</summary>
    public static PolicyConstraint<TAddress> Unconstrained { get; } = new();

    /// <summary>Gets the source addresses to keep, or <see langword="null"/> for the whole address universe.</summary>
    public IntervalSet<TAddress>? Source { get; init; }

    /// <summary>Gets the source ports to keep, or <see langword="null"/> for every modeled port.</summary>
    public IntervalSet<ushort>? SourcePorts { get; init; }

    /// <summary>Gets the destination addresses to keep, or <see langword="null"/> for the whole address universe.</summary>
    public IntervalSet<TAddress>? Destination { get; init; }

    /// <summary>Gets the destination ports to keep, or <see langword="null"/> for every modeled port.</summary>
    public IntervalSet<ushort>? DestinationPorts { get; init; }

    /// <summary>Gets the protocols to keep, or <see langword="null"/> for every protocol in the world.</summary>
    public FiniteSet<ProtocolSymbol>? Protocols { get; init; }

    /// <summary>Gets the ingress interfaces to keep. Null means all known ingress interfaces when the chain has ingress.</summary>
    public FiniteSet<NetworkInterfaceName>? Ingress { get; init; }

    /// <summary>Gets the egress interfaces to keep. Null means all known egress interfaces when the chain has egress.</summary>
    public FiniteSet<NetworkInterfaceName>? Egress { get; init; }
}
