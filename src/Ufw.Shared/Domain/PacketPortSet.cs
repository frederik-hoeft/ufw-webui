using System.Numerics;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Set-valued packet port axis. Protocols without port semantics use the distinct not-applicable value.
/// </summary>
public readonly struct PacketPortSet : IDimensionSet, IEquatable<PacketPortSet>
{
    private PacketPortSet(IntervalSet<ushort> ports, bool includesNotApplicable)
    {
        if (!PacketPorts.Universe.IsSupersetOf(ports))
        {
            throw new ArgumentException("Port values must be between 1 and 65535.", nameof(ports));
        }

        Ports = ports;
        IncludesNotApplicable = includesNotApplicable;
    }

    /// <summary>Gets the empty set.</summary>
    public static PacketPortSet Empty => default;

    /// <summary>Gets every numeric port.</summary>
    public static PacketPortSet PortUniverse { get; } = FromPorts(PacketPorts.Universe);

    /// <summary>Gets the single not-applicable value used by protocols without ports.</summary>
    public static PacketPortSet NotApplicable { get; } = new(IntervalSet<ushort>.Empty, includesNotApplicable: true);

    /// <summary>Gets both every numeric port and the not-applicable value.</summary>
    public static PacketPortSet Complete { get; } = new(PacketPorts.Universe, includesNotApplicable: true);

    /// <summary>Gets the numeric ports in the set.</summary>
    public IntervalSet<ushort> Ports { get; }

    /// <summary>Gets a value indicating whether the set contains the portless-protocol value.</summary>
    public bool IncludesNotApplicable { get; }

    /// <summary>Gets a value indicating whether the set is empty.</summary>
    public bool IsEmpty => Ports.IsEmpty && !IncludesNotApplicable;

    /// <summary>Gets the number of values in the set.</summary>
    public BigInteger Cardinality => Ports.Cardinality + (IncludesNotApplicable ? BigInteger.One : BigInteger.Zero);

    /// <summary>Creates a set containing numeric ports.</summary>
    public static PacketPortSet FromPorts(IntervalSet<ushort> ports) => new(ports, includesNotApplicable: false);

    /// <summary>Returns whether the nullable point value belongs to the set.</summary>
    public bool Contains(ushort? port) => port is null ? IncludesNotApplicable : Ports.Contains(port.Value);

    /// <summary>Returns the intersection.</summary>
    public PacketPortSet Intersect(PacketPortSet other) => new(Ports.Intersect(other.Ports), IncludesNotApplicable && other.IncludesNotApplicable);

    /// <summary>Returns the union.</summary>
    public PacketPortSet Union(PacketPortSet other) => new(Ports.Union(other.Ports), IncludesNotApplicable || other.IncludesNotApplicable);

    /// <summary>Returns <c>this \ other</c>.</summary>
    public PacketPortSet Except(PacketPortSet other) => new(Ports.Except(other.Ports), IncludesNotApplicable && !other.IncludesNotApplicable);

    /// <summary>Returns whether the intersection is non-empty.</summary>
    public bool Overlaps(PacketPortSet other) => (IncludesNotApplicable && other.IncludesNotApplicable) || Ports.Overlaps(other.Ports);

    /// <summary>Returns whether every value of <paramref name="other"/> is contained.</summary>
    public bool IsSupersetOf(PacketPortSet other) => (!other.IncludesNotApplicable || IncludesNotApplicable) && Ports.IsSupersetOf(other.Ports);

    /// <inheritdoc />
    public int CompareTo(PacketPortSet other)
    {
        int compared = IncludesNotApplicable.CompareTo(other.IncludesNotApplicable);
        return compared != 0 ? compared : Ports.CompareTo(other.Ports);
    }

    /// <inheritdoc />
    public bool Equals(PacketPortSet other) => IncludesNotApplicable == other.IncludesNotApplicable && Ports.Equals(other.Ports);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PacketPortSet other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Ports, IncludesNotApplicable);

    /// <inheritdoc />
    public override string ToString()
    {
        if (!IncludesNotApplicable)
        {
            return Ports.ToString();
        }

        return Ports.IsEmpty ? "{n/a}" : $"{Ports} ∪ {{n/a}}";
    }

    int IDimensionSet.CompareTo(IDimensionSet other) => CompareTo(Expect(other));

    bool IDimensionSet.SetEquals(IDimensionSet other) => Equals(Expect(other));

    bool IDimensionSet.Overlaps(IDimensionSet other) => Overlaps(Expect(other));

    bool IDimensionSet.Contains(IDimensionSet other) => IsSupersetOf(Expect(other));

    IDimensionSet IDimensionSet.Intersect(IDimensionSet other) => Intersect(Expect(other));

    IDimensionSet IDimensionSet.Except(IDimensionSet other) => Except(Expect(other));

    IDimensionSet IDimensionSet.Union(IDimensionSet other) => Union(Expect(other));

    private static PacketPortSet Expect(IDimensionSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other is PacketPortSet set
            ? set
            : throw new ArgumentException($"Expected a {nameof(PacketPortSet)} axis.", nameof(other));
    }
}
