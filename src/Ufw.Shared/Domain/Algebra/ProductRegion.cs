using System.Numerics;

namespace Ufw.Shared.Domain.Algebra;

/// <summary>
/// One factored Cartesian product <c>A0 × A1 × ... × An</c>, with one set-valued component per axis.
/// </summary>
/// <remarks>
/// "Rectangle" is combinatorial rather than necessarily geometrically connected: an individual axis set may
/// itself contain multiple disjoint intervals or symbols. The region still represents exactly the Cartesian
/// product of its axis sets.
/// </remarks>
public sealed class ProductRegion
{
    private readonly IDimensionSet[] _axes;

    /// <summary>
    /// Creates a rectangle. Each axis is copied by reference; dimension sets are immutable.
    /// </summary>
    public ProductRegion(IReadOnlyList<IDimensionSet> axes)
    {
        ArgumentNullException.ThrowIfNull(axes);
        if (axes.Count == 0)
        {
            throw new ArgumentException("A product region needs at least one axis.", nameof(axes));
        }

        IDimensionSet[] copy = new IDimensionSet[axes.Count];
        for (int index = 0; index < axes.Count; index++)
        {
            copy[index] = axes[index] ?? throw new ArgumentException("A product axis cannot be null.", nameof(axes));
        }

        _axes = copy;
    }

    /// <summary>Gets the number of axes.</summary>
    public int DimensionCount => _axes.Length;

    /// <summary>Gets a value indicating whether any axis is empty, which makes the product empty.</summary>
    public bool IsEmpty
    {
        get
        {
            foreach (IDimensionSet axis in _axes)
            {
                if (axis.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Gets the number of tuples in the product.</summary>
    public BigInteger Cardinality
    {
        get
        {
            if (IsEmpty)
            {
                return BigInteger.Zero;
            }

            BigInteger product = BigInteger.One;
            foreach (IDimensionSet axis in _axes)
            {
                product *= axis.Cardinality;
            }

            return product;
        }
    }

    /// <summary>Gets the set stored on <paramref name="axis"/>.</summary>
    public IDimensionSet Axis(int axis)
    {
        if ((uint)axis >= (uint)_axes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(axis));
        }

        return _axes[axis];
    }

    /// <summary>Returns a copy with one axis replaced.</summary>
    public ProductRegion WithAxis(int axis, IDimensionSet value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if ((uint)axis >= (uint)_axes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(axis));
        }

        IDimensionSet[] copy = (IDimensionSet[])_axes.Clone();
        copy[axis] = value;
        return new ProductRegion(copy);
    }

    /// <summary>Returns the component-wise intersection. The result is empty when any axis misses.</summary>
    public ProductRegion Intersect(ProductRegion other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameRank(other);
        IDimensionSet[] axes = new IDimensionSet[_axes.Length];
        for (int index = 0; index < _axes.Length; index++)
        {
            axes[index] = _axes[index].Intersect(other._axes[index]);
        }

        return new ProductRegion(axes);
    }

    /// <summary>Returns <see langword="true"/> when every axis pair has a non-empty intersection.</summary>
    public bool Overlaps(ProductRegion other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameRank(other);
        if (IsEmpty || other.IsEmpty)
        {
            return false;
        }

        for (int index = 0; index < _axes.Length; index++)
        {
            if (!_axes[index].Overlaps(other._axes[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a disjoint union of rectangles equal to <c>this \ other</c>.
    /// </summary>
    /// <remarks>
    /// For the first differing axis <c>i</c>, earlier axes keep the intersection and later axes keep this region's original set.
    /// That decomposition covers every tuple that fails to match <paramref name="other"/> exactly once.
    /// </remarks>
    public IReadOnlyList<ProductRegion> Except(ProductRegion other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameRank(other);
        if (IsEmpty)
        {
            return [];
        }

        if (other.IsEmpty || !Overlaps(other))
        {
            return [this];
        }

        IDimensionSet[] intersections = new IDimensionSet[_axes.Length];
        for (int index = 0; index < _axes.Length; index++)
        {
            intersections[index] = _axes[index].Intersect(other._axes[index]);
        }

        List<ProductRegion> pieces = [];
        for (int index = 0; index < _axes.Length; index++)
        {
            IDimensionSet difference = _axes[index].Except(other._axes[index]);
            if (difference.IsEmpty)
            {
                continue;
            }

            IDimensionSet[] piece = new IDimensionSet[_axes.Length];
            for (int earlier = 0; earlier < index; earlier++)
            {
                piece[earlier] = intersections[earlier];
            }

            piece[index] = difference;
            for (int later = index + 1; later < _axes.Length; later++)
            {
                piece[later] = _axes[later];
            }

            pieces.Add(new ProductRegion(piece));
        }

        return pieces;
    }

    /// <summary>Compares rectangles lexicographically by axis.</summary>
    public static int Compare(ProductRegion left, ProductRegion right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        int shared = Math.Min(left._axes.Length, right._axes.Length);
        for (int index = 0; index < shared; index++)
        {
            int compared = left._axes[index].CompareTo(right._axes[index]);
            if (compared != 0)
            {
                return compared;
            }
        }

        return left._axes.Length.CompareTo(right._axes.Length);
    }

    /// <inheritdoc />
    public override string ToString() => string.Join(" x ", _axes);

    private void EnsureSameRank(ProductRegion other)
    {
        if (_axes.Length != other._axes.Length)
        {
            throw new ArgumentException("Product regions must have the same number of axes.", nameof(other));
        }
    }
}
