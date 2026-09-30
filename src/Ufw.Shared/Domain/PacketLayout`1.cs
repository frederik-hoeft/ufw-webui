using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Binds named packet axes to the generic product engine. Axis order is source, source port, destination,
/// destination port, protocol, then ingress and/or egress as the chain profile requires.
/// </summary>
internal sealed class PacketLayout<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
{
    private PacketLayout(PolicyWorld<TAddress> world, ChainProfile profile, IDimensionSet[] universes)
    {
        World = world;
        Profile = profile;
        Universes = universes;
        IngressAxis = profile.HasIngress ? 5 : null;
        EgressAxis = profile.HasEgress ? (profile.HasIngress ? 6 : 5) : null;
    }

    public PolicyWorld<TAddress> World { get; }

    public ChainProfile Profile { get; }

    public IDimensionSet[] Universes { get; }

    public int? IngressAxis { get; }

    public int? EgressAxis { get; }

    public static PacketLayout<TAddress> Create(PolicyWorld<TAddress> world, ChainProfile profile)
    {
        List<IDimensionSet> axes =
        [
            world.AddressUniverse,
            world.PortUniverse,
            world.AddressUniverse,
            world.PortUniverse,
            world.Protocols,
        ];
        if (profile.HasIngress)
        {
            axes.Add(world.Interfaces);
        }

        if (profile.HasEgress)
        {
            axes.Add(world.Interfaces);
        }

        return new PacketLayout<TAddress>(world, profile, [.. axes]);
    }

    public ProductRegion Materialize(PolicyConstraint<TAddress> constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        IDimensionSet[] axes = new IDimensionSet[Universes.Length];
        axes[0] = Resolve(constraint.Source, AddressUniverse(0), "source address");
        axes[1] = Resolve(constraint.SourcePorts, PortUniverse(1), "source port");
        axes[2] = Resolve(constraint.Destination, AddressUniverse(2), "destination address");
        axes[3] = Resolve(constraint.DestinationPorts, PortUniverse(3), "destination port");
        axes[4] = ResolveFinite(constraint.Protocols, (FiniteSet<ProtocolSymbol>)Universes[4], "protocol");
        if (IngressAxis is int ingressAxis)
        {
            axes[ingressAxis] = ResolveFinite(constraint.Ingress, (FiniteSet<NetworkInterfaceName>)Universes[ingressAxis], "ingress");
        }
        else if (constraint.Ingress is not null)
        {
            throw new ArgumentException("This chain has no ingress interface.");
        }

        if (EgressAxis is int egressAxis)
        {
            axes[egressAxis] = ResolveFinite(constraint.Egress, (FiniteSet<NetworkInterfaceName>)Universes[egressAxis], "egress");
        }
        else if (constraint.Egress is not null)
        {
            throw new ArgumentException("This chain has no egress interface.");
        }

        return new ProductRegion(axes);
    }

    public ProductRegion FromPacket(PacketRegion<TAddress> region)
    {
        ArgumentNullException.ThrowIfNull(region);
        List<IDimensionSet> axes =
        [
            region.Source,
            region.SourcePorts,
            region.Destination,
            region.DestinationPorts,
            region.Protocols,
        ];
        if (Profile.HasIngress)
        {
            if (region.Ingress is null)
            {
                throw new ArgumentException("The packet region is missing its ingress axis.");
            }

            axes.Add(region.Ingress.Value);
        }
        else if (region.Ingress is not null)
        {
            throw new ArgumentException("The packet region has ingress on a chain without it.");
        }

        if (Profile.HasEgress)
        {
            if (region.Egress is null)
            {
                throw new ArgumentException("The packet region is missing its egress axis.");
            }

            axes.Add(region.Egress.Value);
        }
        else if (region.Egress is not null)
        {
            throw new ArgumentException("The packet region has egress on a chain without it.");
        }

        return new ProductRegion(axes);
    }

    public PacketRegion<TAddress> ToPacket(ProductRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        FiniteSet<NetworkInterfaceName>? ingress = IngressAxis is int ingressAxis
            ? (FiniteSet<NetworkInterfaceName>)region.Axis(ingressAxis)
            : null;
        FiniteSet<NetworkInterfaceName>? egress = EgressAxis is int egressAxis
            ? (FiniteSet<NetworkInterfaceName>)region.Axis(egressAxis)
            : null;
        return new PacketRegion<TAddress>(
            (IntervalSet<TAddress>)region.Axis(0),
            (IntervalSet<ushort>)region.Axis(1),
            (IntervalSet<TAddress>)region.Axis(2),
            (IntervalSet<ushort>)region.Axis(3),
            (FiniteSet<ProtocolSymbol>)region.Axis(4),
            ingress,
            egress);
    }

    public void ValidatePoint(PacketPoint<TAddress> point)
    {
        if (!World.AddressUniverse.Contains(point.Source) || !World.AddressUniverse.Contains(point.Destination))
        {
            throw new ArgumentException("The packet address is outside the closed world.");
        }

        if (!World.PortUniverse.Contains(point.SourcePort) || !World.PortUniverse.Contains(point.DestinationPort))
        {
            throw new ArgumentException("The packet port is outside the closed world.");
        }

        if (string.IsNullOrEmpty(point.Protocol.Name) || !World.Protocols.Contains(point.Protocol))
        {
            throw new ArgumentException("The packet protocol is outside the closed world.");
        }

        ValidateInterface(Profile.HasIngress, point.Ingress, "ingress");
        ValidateInterface(Profile.HasEgress, point.Egress, "egress");
    }

    private void ValidateInterface(bool axisExists, NetworkInterfaceName? name, string axis)
    {
        if (!axisExists)
        {
            if (name is not null)
            {
                throw new ArgumentException($"This chain has no {axis} interface.");
            }

            return;
        }

        if (name is null || string.IsNullOrEmpty(name.Value.Name) || !World.Interfaces.Contains(name.Value))
        {
            throw new ArgumentException($"The packet {axis} interface is outside the closed world.");
        }
    }

    private IntervalSet<TAddress> AddressUniverse(int axis) => (IntervalSet<TAddress>)Universes[axis];

    private IntervalSet<ushort> PortUniverse(int axis) => (IntervalSet<ushort>)Universes[axis];

    private static IntervalSet<T> Resolve<T>(IntervalSet<T>? specified, IntervalSet<T> universe, string axis)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        if (specified is null)
        {
            return universe;
        }

        if (!universe.IsSupersetOf(specified.Value))
        {
            throw new ArgumentException($"The {axis} constraint is outside the closed world.");
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
            throw new ArgumentException($"The {axis} constraint is outside the closed world.");
        }

        return specified.Value;
    }
}
