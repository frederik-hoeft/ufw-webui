using System.Numerics;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// One rectangle of packet space. Null interface sets mean that axis is not part of the chain, not that every interface matches.
/// </summary>
public sealed class PacketRegion<TAddress> : IEquatable<PacketRegion<TAddress>>
    where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
{
    /// <summary>Creates a rectangle from already canonical axis sets.</summary>
    public PacketRegion(
        IntervalSet<TAddress> source,
        IntervalSet<ushort> sourcePorts,
        IntervalSet<TAddress> destination,
        IntervalSet<ushort> destinationPorts,
        FiniteSet<ProtocolSymbol> protocols,
        FiniteSet<NetworkInterfaceName>? ingress,
        FiniteSet<NetworkInterfaceName>? egress)
    {
        Source = source;
        SourcePorts = sourcePorts;
        Destination = destination;
        DestinationPorts = destinationPorts;
        Protocols = protocols;
        Ingress = ingress;
        Egress = egress;
    }

    /// <summary>Gets the source addresses.</summary>
    public IntervalSet<TAddress> Source { get; }

    /// <summary>Gets the source ports.</summary>
    public IntervalSet<ushort> SourcePorts { get; }

    /// <summary>Gets the destination addresses.</summary>
    public IntervalSet<TAddress> Destination { get; }

    /// <summary>Gets the destination ports.</summary>
    public IntervalSet<ushort> DestinationPorts { get; }

    /// <summary>Gets the protocols.</summary>
    public FiniteSet<ProtocolSymbol> Protocols { get; }

    /// <summary>Gets the ingress interfaces, or <see langword="null"/> when the chain has no ingress axis.</summary>
    public FiniteSet<NetworkInterfaceName>? Ingress { get; }

    /// <summary>Gets the egress interfaces, or <see langword="null"/> when the chain has no egress axis.</summary>
    public FiniteSet<NetworkInterfaceName>? Egress { get; }

    /// <summary>Gets a value indicating whether any present axis is empty.</summary>
    public bool IsEmpty => Cardinality == BigInteger.Zero;

    /// <summary>Gets the number of packets in the rectangle.</summary>
    public BigInteger Cardinality
    {
        get
        {
            BigInteger product = Source.Cardinality
                * SourcePorts.Cardinality
                * Destination.Cardinality
                * DestinationPorts.Cardinality
                * Protocols.Cardinality;
            if (Ingress is { } ingress)
            {
                product *= ingress.Cardinality;
            }

            if (Egress is { } egress)
            {
                product *= egress.Cardinality;
            }

            return product;
        }
    }

    /// <summary>Returns <see langword="true"/> when every component of <paramref name="point"/> lies in this rectangle.</summary>
    public bool Contains(PacketPoint<TAddress> point)
    {
        if (!Source.Contains(point.Source)
            || !SourcePorts.Contains(point.SourcePort)
            || !Destination.Contains(point.Destination)
            || !DestinationPorts.Contains(point.DestinationPort)
            || !Protocols.Contains(point.Protocol))
        {
            return false;
        }

        if (!ContainsInterface(Ingress, point.Ingress) || !ContainsInterface(Egress, point.Egress))
        {
            return false;
        }

        return true;
    }

    /// <summary>Returns <see langword="true"/> when the rectangles share at least one packet.</summary>
    public bool Overlaps(PacketRegion<TAddress> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!Source.Overlaps(other.Source)
            || !SourcePorts.Overlaps(other.SourcePorts)
            || !Destination.Overlaps(other.Destination)
            || !DestinationPorts.Overlaps(other.DestinationPorts)
            || !Protocols.Overlaps(other.Protocols))
        {
            return false;
        }

        return InterfacesOverlap(Ingress, other.Ingress) && InterfacesOverlap(Egress, other.Egress);
    }

    /// <summary>Compares rectangles lexicographically by source, ports, destination, protocol, then interfaces.</summary>
    public int CompareTo(PacketRegion<TAddress> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        int compared = Source.CompareTo(other.Source);
        if (compared != 0)
        {
            return compared;
        }

        compared = SourcePorts.CompareTo(other.SourcePorts);
        if (compared != 0)
        {
            return compared;
        }

        compared = Destination.CompareTo(other.Destination);
        if (compared != 0)
        {
            return compared;
        }

        compared = DestinationPorts.CompareTo(other.DestinationPorts);
        if (compared != 0)
        {
            return compared;
        }

        compared = Protocols.CompareTo(other.Protocols);
        if (compared != 0)
        {
            return compared;
        }

        compared = CompareInterfaces(Ingress, other.Ingress);
        if (compared != 0)
        {
            return compared;
        }

        return CompareInterfaces(Egress, other.Egress);
    }

    /// <inheritdoc />
    public bool Equals(PacketRegion<TAddress>? other)
    {
        if (other is null)
        {
            return false;
        }

        return Source.Equals(other.Source)
            && SourcePorts.Equals(other.SourcePorts)
            && Destination.Equals(other.Destination)
            && DestinationPorts.Equals(other.DestinationPorts)
            && Protocols.Equals(other.Protocols)
            && NullableEquals(Ingress, other.Ingress)
            && NullableEquals(Egress, other.Egress);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PacketRegion<TAddress>);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Source, SourcePorts, Destination, DestinationPorts, Protocols, Ingress, Egress);

    /// <inheritdoc />
    public override string ToString()
    {
        string ingress = Ingress is { } ingressSet ? ingressSet.ToString() : "-";
        string egress = Egress is { } egressSet ? egressSet.ToString() : "-";
        return $"src {Source} sport {SourcePorts} dst {Destination} dport {DestinationPorts} proto {Protocols} in {ingress} out {egress}";
    }

    private static bool ContainsInterface(FiniteSet<NetworkInterfaceName>? region, NetworkInterfaceName? point)
    {
        if (region is null)
        {
            return point is null;
        }

        return point is { } name && region.Value.Contains(name);
    }

    private static bool InterfacesOverlap(FiniteSet<NetworkInterfaceName>? left, FiniteSet<NetworkInterfaceName>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Value.Overlaps(right.Value);
    }

    private static int CompareInterfaces(FiniteSet<NetworkInterfaceName>? left, FiniteSet<NetworkInterfaceName>? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        return left.Value.CompareTo(right.Value);
    }

    private static bool NullableEquals(FiniteSet<NetworkInterfaceName>? left, FiniteSet<NetworkInterfaceName>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.Value.Equals(right.Value);
    }
}
