using System.Numerics;

namespace Ufw.Shared.Domain.Algebra;

/// <summary>
/// Disjoint union of <see cref="ProductRegion"/> values, stored in a factored canonical representation.
/// </summary>
/// <remarks>
/// Regions that are equal on every axis except one can be algebraically factored by unioning that varying axis:
/// <c>(A × X) ∪ (B × X) = (A ∪ B) × X</c>. Coalescing applies that identity without changing the represented
/// tuple set or introducing overlap.
/// </remarks>
public sealed class ProductSpace
{
    private readonly ProductRegion[] _regions;

    private ProductSpace(ProductRegion[] regions)
    {
        _regions = regions;
    }

    /// <summary>Gets the empty space. It has no axes until it is combined with a region.</summary>
    public static ProductSpace Empty { get; } = new([]);

    /// <summary>Gets a value indicating whether the space contains no tuples.</summary>
    public bool IsEmpty => _regions.Length == 0;

    /// <summary>Gets the disjoint rectangles in canonical axis order.</summary>
    public IReadOnlyList<ProductRegion> Regions => _regions;

    /// <summary>Gets the number of tuples in the space.</summary>
    public BigInteger Cardinality
    {
        get
        {
            BigInteger total = BigInteger.Zero;
            foreach (ProductRegion region in _regions)
            {
                total += region.Cardinality;
            }

            return total;
        }
    }

    /// <summary>
    /// Builds a space from product regions, dropping empty regions, requiring a common rank and pairwise disjointness,
    /// then coalescing regions that can be factored without changing the represented tuple set.
    /// </summary>
    /// <remarks>
    /// Disjointness is checked before coalescing so an invalid overlapping input cannot be hidden by a later union
    /// of one axis.
    /// </remarks>
    public static ProductSpace FromRegions(IEnumerable<ProductRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        List<ProductRegion> materialized = [];
        int? rank = null;
        foreach (ProductRegion region in regions)
        {
            ArgumentNullException.ThrowIfNull(region);
            if (region.IsEmpty)
            {
                continue;
            }

            if (rank is null)
            {
                rank = region.DimensionCount;
            }
            else if (rank.Value != region.DimensionCount)
            {
                throw new ArgumentException("Product regions in one space must have the same number of axes.", nameof(regions));
            }

            materialized.Add(region);
        }

        EnsureDisjoint(materialized);
        ProductRegion[] coalesced = Coalesce(materialized);
        return new ProductSpace(coalesced);
    }

    /// <summary>Returns the tuples that also lie in <paramref name="cut"/>.</summary>
    public ProductSpace Intersect(ProductRegion cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        if (IsEmpty || cut.IsEmpty)
        {
            return Empty;
        }

        List<ProductRegion> result = [];
        foreach (ProductRegion region in _regions)
        {
            ProductRegion hit = region.Intersect(cut);
            if (!hit.IsEmpty)
            {
                result.Add(hit);
            }
        }

        return FromRegions(result);
    }

    /// <summary>Returns the tuples that lie in both spaces.</summary>
    public ProductSpace Intersect(ProductSpace other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsEmpty || other.IsEmpty)
        {
            return Empty;
        }

        List<ProductRegion> result = [];
        foreach (ProductRegion left in _regions)
        {
            foreach (ProductRegion right in other._regions)
            {
                ProductRegion hit = left.Intersect(right);
                if (!hit.IsEmpty)
                {
                    result.Add(hit);
                }
            }
        }

        return FromRegions(result);
    }

    /// <summary>Returns the tuples of this space that are outside <paramref name="cut"/>.</summary>
    public ProductSpace Except(ProductRegion cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        if (IsEmpty || cut.IsEmpty)
        {
            return this;
        }

        List<ProductRegion> result = [];
        foreach (ProductRegion region in _regions)
        {
            foreach (ProductRegion piece in region.Except(cut))
            {
                if (!piece.IsEmpty)
                {
                    result.Add(piece);
                }
            }
        }

        return FromRegions(result);
    }

    /// <summary>Returns the tuples of this space that are outside <paramref name="other"/>.</summary>
    public ProductSpace Except(ProductSpace other)
    {
        ArgumentNullException.ThrowIfNull(other);
        ProductSpace remaining = this;
        foreach (ProductRegion region in other._regions)
        {
            remaining = remaining.Except(region);
        }

        return remaining;
    }

    /// <summary>Returns the disjoint union. Overlap is kept once.</summary>
    public ProductSpace Union(ProductSpace other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        ProductSpace added = other;
        foreach (ProductRegion region in _regions)
        {
            added = added.Except(region);
        }

        List<ProductRegion> combined = new(_regions.Length + added._regions.Length);
        combined.AddRange(_regions);
        combined.AddRange(added._regions);
        return FromRegions(combined);
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="region"/> shares a tuple with this space.</summary>
    public bool Overlaps(ProductRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        foreach (ProductRegion existing in _regions)
        {
            if (existing.Overlaps(region))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Repeatedly factors regions that are equal on all but one axis until no further representation reduction is possible.
    /// </summary>
    /// <remarks>
    /// A merge on one axis can expose a merge on an axis processed earlier in the same pass, so coalescing iterates to
    /// a fixed point rather than performing a single sweep.
    /// </remarks>
    private static ProductRegion[] Coalesce(List<ProductRegion> regions)
    {
        if (regions.Count == 0)
        {
            return [];
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            int dimensions = regions[0].DimensionCount;
            for (int axis = 0; axis < dimensions; axis++)
            {
                if (MergeAxis(regions, axis))
                {
                    changed = true;
                }
            }
        }

        regions.Sort(ProductRegion.Compare);
        return [.. regions];
    }

    /// <summary>
    /// Factors one axis by grouping regions whose remaining axes are set-equal, then unioning the selected axis inside each group.
    /// </summary>
    private static bool MergeAxis(List<ProductRegion> regions, int axis)
    {
        Dictionary<FixedAxesKey, int> groups = new();
        List<ProductRegion> next = new(regions.Count);
        bool merged = false;
        foreach (ProductRegion region in regions)
        {
            FixedAxesKey key = new(region, axis);
            if (groups.TryGetValue(key, out int index))
            {
                ProductRegion existing = next[index];
                next[index] = existing.WithAxis(axis, existing.Axis(axis).Union(region.Axis(axis)));
                merged = true;
            }
            else
            {
                groups.Add(key, next.Count);
                next.Add(region);
            }
        }

        if (!merged)
        {
            return false;
        }

        regions.Clear();
        regions.AddRange(next);
        return true;
    }

    private static void EnsureDisjoint(IReadOnlyList<ProductRegion> regions)
    {
        for (int left = 0; left < regions.Count; left++)
        {
            for (int right = left + 1; right < regions.Count; right++)
            {
                if (regions[left].Overlaps(regions[right]))
                {
                    throw new InvalidOperationException("Product space regions overlap.");
                }
            }
        }
    }

    /// <summary>
    /// Hash/equality key for the axes that stay fixed while one selected axis is being coalesced.
    /// Two keys are equal when they ignore the same varying axis and all remaining axes are set-equal.
    /// </summary>
    private readonly struct FixedAxesKey : IEquatable<FixedAxesKey>
    {
        private readonly ProductRegion _region;
        private readonly int _axis;

        public FixedAxesKey(ProductRegion region, int axis)
        {
            _region = region;
            _axis = axis;
        }

        public bool Equals(FixedAxesKey other)
        {
            if (_axis != other._axis || _region.DimensionCount != other._region.DimensionCount)
            {
                return false;
            }

            for (int index = 0; index < _region.DimensionCount; index++)
            {
                if (index == _axis)
                {
                    continue;
                }

                if (!_region.Axis(index).SetEquals(other._region.Axis(index)))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is FixedAxesKey other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(_axis);
            for (int index = 0; index < _region.DimensionCount; index++)
            {
                if (index == _axis)
                {
                    continue;
                }

                hash.Add(_region.Axis(index).GetHashCode());
            }

            return hash.ToHashCode();
        }
    }
}
