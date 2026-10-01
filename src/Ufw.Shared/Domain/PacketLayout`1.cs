using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Binds named packet axes to the generic product engine. Axis order is source, source port, destination,
/// destination port, protocol, then ingress and/or egress as the chain profile requires.
/// </summary>
internal sealed class PacketLayout<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
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
        ArgumentNullException.ThrowIfNull(world);
        List<IDimensionSet> axes =
        [
            world.AddressUniverse,
            PacketPortSet.Complete,
            world.AddressUniverse,
            PacketPortSet.Complete,
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

    public ProductSpace Materialize(PolicyConstraint<TAddress> constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        IntervalSet<TAddress> source = Resolve(constraint.Source, World.AddressUniverse, "source address");
        IntervalSet<TAddress> destination = Resolve(constraint.Destination, World.AddressUniverse, "destination address");
        FiniteSet<ProtocolSymbol> protocols = ResolveFinite(constraint.Protocols, World.Protocols, "protocol");
        FiniteSet<NetworkInterfaceName>? ingress = ResolveInterface(Profile.HasIngress, constraint.Ingress, "ingress");
        FiniteSet<NetworkInterfaceName>? egress = ResolveInterface(Profile.HasEgress, constraint.Egress, "egress");

        List<ProtocolSymbol> portBearing = [];
        List<ProtocolSymbol> portless = [];
        foreach (ProtocolSymbol protocol in protocols.Values)
        {
            (World.ProtocolUsesPorts(protocol) ? portBearing : portless).Add(protocol);
        }

        List<ProductRegion> regions = [];
        if (portBearing.Count != 0)
        {
            PacketPortSet sourcePorts = PacketPortSet.FromPorts(ResolvePorts(constraint.SourcePorts, "source port"));
            PacketPortSet destinationPorts = PacketPortSet.FromPorts(ResolvePorts(constraint.DestinationPorts, "destination port"));
            regions.Add(CreateRegion(source, sourcePorts, destination, destinationPorts, FiniteSet<ProtocolSymbol>.From(portBearing), ingress, egress));
        }

        if (portless.Count != 0 && constraint.SourcePorts is null && constraint.DestinationPorts is null)
        {
            regions.Add(CreateRegion(source, PacketPortSet.NotApplicable, destination, PacketPortSet.NotApplicable, FiniteSet<ProtocolSymbol>.From(portless), ingress, egress));
        }

        return ProductSpace.FromRegions(regions);
    }

    public ProductRegion FromPacket(PacketRegion<TAddress> region)
    {
        ArgumentNullException.ThrowIfNull(region);
        ValidateRegion(region);
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
                throw new ArgumentException("The packet region is missing its ingress axis.", nameof(region));
            }

            axes.Add(region.Ingress.Value);
        }
        else if (region.Ingress is not null)
        {
            throw new ArgumentException("The packet region has ingress on a chain without it.", nameof(region));
        }

        if (Profile.HasEgress)
        {
            if (region.Egress is null)
            {
                throw new ArgumentException("The packet region is missing its egress axis.", nameof(region));
            }

            axes.Add(region.Egress.Value);
        }
        else if (region.Egress is not null)
        {
            throw new ArgumentException("The packet region has egress on a chain without it.", nameof(region));
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
            (PacketPortSet)region.Axis(1),
            (IntervalSet<TAddress>)region.Axis(2),
            (PacketPortSet)region.Axis(3),
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

        if (string.IsNullOrEmpty(point.Protocol.Name) || !World.Protocols.Contains(point.Protocol))
        {
            throw new ArgumentException("The packet protocol is outside the closed world.");
        }

        if (World.ProtocolUsesPorts(point.Protocol))
        {
            if (point.SourcePort is not ushort sourcePort || point.DestinationPort is not ushort destinationPort
                || !World.PortUniverse.Contains(sourcePort) || !World.PortUniverse.Contains(destinationPort))
            {
                throw new ArgumentException("Port-bearing protocols require source and destination ports inside the closed world.");
            }
        }
        else if (point.SourcePort is not null || point.DestinationPort is not null)
        {
            throw new ArgumentException("Protocols without port semantics cannot carry source or destination ports.");
        }

        ValidateInterface(Profile.HasIngress, point.Ingress, "ingress");
        ValidateInterface(Profile.HasEgress, point.Egress, "egress");
    }

    public bool IsCompatibleWith(PacketLayout<TAddress> other) =>
        other is not null && Profile.Chain == other.Profile.Chain && World.HasEquivalentPacketUniverse(other.World);

    private void ValidateRegion(PacketRegion<TAddress> region)
    {
        if (!World.AddressUniverse.IsSupersetOf(region.Source) || !World.AddressUniverse.IsSupersetOf(region.Destination))
        {
            throw new ArgumentException("The packet region contains addresses outside the closed world.", nameof(region));
        }

        if (!World.Protocols.IsSupersetOf(region.Protocols))
        {
            throw new ArgumentException("The packet region contains protocols outside the closed world.", nameof(region));
        }

        ValidateRegionInterface(Profile.HasIngress, region.Ingress, "ingress", region);
        ValidateRegionInterface(Profile.HasEgress, region.Egress, "egress", region);
        if (region.IsEmpty)
        {
            return;
        }

        bool hasPortBearing = false;
        bool hasPortless = false;
        foreach (ProtocolSymbol protocol in region.Protocols.Values)
        {
            if (World.ProtocolUsesPorts(protocol))
            {
                hasPortBearing = true;
            }
            else
            {
                hasPortless = true;
            }
        }

        if (hasPortBearing && hasPortless)
        {
            throw new ArgumentException("One packet rectangle cannot mix protocols with different port applicability.", nameof(region));
        }

        if (hasPortBearing && (region.SourcePorts.IncludesNotApplicable || region.DestinationPorts.IncludesNotApplicable))
        {
            throw new ArgumentException("Port-bearing protocols cannot use not-applicable port values.", nameof(region));
        }

        if (hasPortless
            && (!region.SourcePorts.Equals(PacketPortSet.NotApplicable) || !region.DestinationPorts.Equals(PacketPortSet.NotApplicable)))
        {
            throw new ArgumentException("Protocols without port semantics require not-applicable source and destination ports.", nameof(region));
        }
    }

    private void ValidateRegionInterface(
        bool axisExists,
        FiniteSet<NetworkInterfaceName>? specified,
        string axis,
        PacketRegion<TAddress> region)
    {
        if (!axisExists)
        {
            if (specified is not null)
            {
                throw new ArgumentException($"The packet region has {axis} on a chain without it.", nameof(region));
            }

            return;
        }

        if (specified is null)
        {
            throw new ArgumentException($"The packet region is missing its {axis} axis.", nameof(region));
        }

        if (!World.Interfaces.IsSupersetOf(specified.Value))
        {
            throw new ArgumentException($"The packet region contains {axis} interfaces outside the closed world.", nameof(region));
        }
    }

    private ProductRegion CreateRegion(
        IntervalSet<TAddress> source,
        PacketPortSet sourcePorts,
        IntervalSet<TAddress> destination,
        PacketPortSet destinationPorts,
        FiniteSet<ProtocolSymbol> protocols,
        FiniteSet<NetworkInterfaceName>? ingress,
        FiniteSet<NetworkInterfaceName>? egress)
    {
        List<IDimensionSet> axes = [source, sourcePorts, destination, destinationPorts, protocols];
        if (IngressAxis is not null)
        {
            axes.Add(ingress!.Value);
        }

        if (EgressAxis is not null)
        {
            axes.Add(egress!.Value);
        }

        return new ProductRegion(axes);
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

    private FiniteSet<NetworkInterfaceName>? ResolveInterface(bool axisExists, FiniteSet<NetworkInterfaceName>? specified, string axis)
    {
        if (!axisExists)
        {
            if (specified is not null)
            {
                throw new ArgumentException($"This chain has no {axis} interface.");
            }

            return null;
        }

        return ResolveFinite(specified, World.Interfaces, axis);
    }

    private static IntervalSet<ushort> ResolvePorts(IntervalSet<ushort>? specified, string axis)
    {
        if (specified is null)
        {
            return PacketPorts.Universe;
        }

        if (!PacketPorts.Universe.IsSupersetOf(specified.Value))
        {
            throw new ArgumentException($"The {axis} constraint is outside the closed world.");
        }

        return specified.Value;
    }

    private static IntervalSet<T> Resolve<T>(IntervalSet<T>? specified, IntervalSet<T> universe, string axis)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
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
