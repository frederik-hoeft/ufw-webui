using System.Numerics;

namespace Ufw.Shared.Domain.Algebra;

/// <summary>
/// Canonical subset of one packet-space axis.
/// </summary>
/// <remarks>
/// Implementations are immutable. Set arguments must be the same concrete kind of axis; a mismatch is a programming error.
/// <see cref="Contains"/> means "this set is a superset of the argument", not element membership.
/// </remarks>
public interface IDimensionSet
{
    /// <summary>Gets a value indicating whether the set contains no values.</summary>
    bool IsEmpty { get; }

    /// <summary>Gets the number of discrete values in the set.</summary>
    BigInteger Cardinality { get; }

    /// <summary>Returns a negative, zero, or positive value that totally orders sets of the same kind.</summary>
    int CompareTo(IDimensionSet other);

    /// <summary>Returns <see langword="true"/> when the sets contain the same values.</summary>
    bool SetEquals(IDimensionSet other);

    /// <summary>Returns <see langword="true"/> when the intersection is non-empty.</summary>
    bool Overlaps(IDimensionSet other);

    /// <summary>Returns <see langword="true"/> when every value of <paramref name="other"/> is in this set.</summary>
    bool Contains(IDimensionSet other);

    /// <summary>Returns the intersection.</summary>
    IDimensionSet Intersect(IDimensionSet other);

    /// <summary>Returns the set difference <c>this \ other</c>.</summary>
    IDimensionSet Except(IDimensionSet other);

    /// <summary>Returns the union.</summary>
    IDimensionSet Union(IDimensionSet other);
}
