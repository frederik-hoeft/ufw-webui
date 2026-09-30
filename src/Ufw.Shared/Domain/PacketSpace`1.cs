using System.Numerics;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Disjoint packet rectangles for one chain. Projections are existential: a value is present when at least one packet in the space uses it.
/// </summary>
public sealed class PacketSpace<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
{
    private readonly PacketLayout<TAddress> _layout;
    private readonly ProductSpace _space;

    internal PacketSpace(PacketLayout<TAddress> layout, ProductSpace space)
    {
        _layout = layout;
        _space = space;
    }

    /// <summary>Gets a value indicating whether the space contains no packets.</summary>
    public bool IsEmpty => _space.IsEmpty;

    /// <summary>Gets the number of packets.</summary>
    public BigInteger Cardinality => _space.Cardinality;

    /// <summary>Gets the disjoint rectangles in canonical order.</summary>
    public IReadOnlyList<PacketRegion<TAddress>> Regions
    {
        get
        {
            PacketRegion<TAddress>[] regions = new PacketRegion<TAddress>[_space.Regions.Count];
            for (int index = 0; index < regions.Length; index++)
            {
                regions[index] = _layout.ToPacket(_space.Regions[index]);
            }

            return regions;
        }
    }

    /// <summary>Builds a space from one rectangle of <paramref name="chain"/>.</summary>
    public static PacketSpace<TAddress> FromRegion(PolicyWorld<TAddress> world, TrafficChain chain, PacketRegion<TAddress> region)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(region);
        PacketLayout<TAddress> layout = PacketLayout<TAddress>.Create(world, ChainProfile.For(chain));
        return new PacketSpace<TAddress>(layout, ProductSpace.FromRegions([layout.FromPacket(region)]));
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="point"/> is in some rectangle.</summary>
    public bool Contains(PacketPoint<TAddress> point)
    {
        _layout.ValidatePoint(point);
        foreach (PacketRegion<TAddress> region in Regions)
        {
            if (region.Contains(point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the packets that also satisfy <paramref name="constraint"/>.</summary>
    public PacketSpace<TAddress> Intersect(PolicyConstraint<TAddress> constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        return new PacketSpace<TAddress>(_layout, _space.Intersect(_layout.Materialize(constraint)));
    }

    /// <summary>Returns the packets that are outside <paramref name="region"/>.</summary>
    public PacketSpace<TAddress> Except(PacketRegion<TAddress> region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return new PacketSpace<TAddress>(_layout, _space.Except(_layout.FromPacket(region)));
    }

    /// <summary>Returns the packets that are outside <paramref name="other"/>.</summary>
    public PacketSpace<TAddress> Except(PacketSpace<TAddress> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!ReferenceEquals(_layout.World, other._layout.World) || _layout.Profile.Chain != other._layout.Profile.Chain)
        {
            throw new ArgumentException("Packet spaces can only be subtracted when they share a world and chain.", nameof(other));
        }

        return new PacketSpace<TAddress>(_layout, _space.Except(other._space));
    }

    /// <summary>Gets every source address that appears in at least one packet.</summary>
    public IntervalSet<TAddress> ProjectSourceAddresses() => ProjectAddresses(0);

    /// <summary>Gets every source port that appears in at least one packet.</summary>
    public IntervalSet<ushort> ProjectSourcePorts() => ProjectPorts(1);

    /// <summary>Gets every destination address that appears in at least one packet.</summary>
    public IntervalSet<TAddress> ProjectDestinationAddresses() => ProjectAddresses(2);

    /// <summary>Gets every destination port that appears in at least one packet.</summary>
    public IntervalSet<ushort> ProjectDestinationPorts() => ProjectPorts(3);

    /// <summary>Gets every protocol that appears in at least one packet.</summary>
    public FiniteSet<ProtocolSymbol> ProjectProtocols() => ProjectSymbols<ProtocolSymbol>(4);

    /// <summary>Gets every ingress interface that appears in at least one packet, or <see langword="null"/> when the chain has no ingress.</summary>
    public FiniteSet<NetworkInterfaceName>? ProjectIngress() =>
        _layout.IngressAxis is int axis ? ProjectSymbols<NetworkInterfaceName>(axis) : null;

    /// <summary>Gets every egress interface that appears in at least one packet, or <see langword="null"/> when the chain has no egress.</summary>
    public FiniteSet<NetworkInterfaceName>? ProjectEgress() =>
        _layout.EgressAxis is int axis ? ProjectSymbols<NetworkInterfaceName>(axis) : null;

    private IntervalSet<TAddress> ProjectAddresses(int axis)
    {
        IntervalSet<TAddress> union = IntervalSet<TAddress>.Empty;
        foreach (ProductRegion region in _space.Regions)
        {
            union = union.Union((IntervalSet<TAddress>)region.Axis(axis));
        }

        return union;
    }

    private IntervalSet<ushort> ProjectPorts(int axis)
    {
        IntervalSet<ushort> union = IntervalSet<ushort>.Empty;
        foreach (ProductRegion region in _space.Regions)
        {
            union = union.Union((IntervalSet<ushort>)region.Axis(axis));
        }

        return union;
    }

    private FiniteSet<TSymbol> ProjectSymbols<TSymbol>(int axis)
        where TSymbol : IEquatable<TSymbol>, IComparable<TSymbol>
    {
        FiniteSet<TSymbol> union = FiniteSet<TSymbol>.Empty;
        foreach (ProductRegion region in _space.Regions)
        {
            union = union.Union((FiniteSet<TSymbol>)region.Axis(axis));
        }

        return union;
    }
}
