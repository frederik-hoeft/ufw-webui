using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Exact partition of one query by how two policy worlds differ.
/// </summary>
/// <remarks>
/// Every queried packet belongs to one cell. Cells do not overlap, and together they are the whole query.
/// Decision changes and responsibility shifts are separate queries over that partition, so they may overlap.
/// <see cref="IsEmpty"/> means the query contained no packets. It does not mean the worlds agree:
/// agreement is a non-empty partition whose cells are <see cref="PolicyDiffKind.Unchanged"/>,
/// or <see cref="Changed"/> being empty.
/// </remarks>
public sealed class PolicyDiff<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
{
    private readonly PacketLayout<TAddress> _layout;
    private readonly ProductSpace _coverage;
    private readonly PolicyDiffCell<TAddress>[] _cells;

    private PolicyDiff(PacketLayout<TAddress> layout, ProductSpace coverage, PolicyDiffCell<TAddress>[] cells)
    {
        _layout = layout;
        _coverage = coverage;
        _cells = cells;
        Cells = Array.AsReadOnly(cells);
    }

    /// <summary>Gets the chain that was compared.</summary>
    public TrafficChain Chain => _layout.Profile.Chain;

    /// <summary>Gets the address family that was compared.</summary>
    public IpFamily Family => _layout.World.Family;

    /// <summary>Gets the cells in canonical region order, including packets where nothing differs.</summary>
    public IReadOnlyList<PolicyDiffCell<TAddress>> Cells { get; }

    /// <summary>Gets a value indicating whether the query contained no packets.</summary>
    public bool IsEmpty => _cells.Length == 0;

    /// <summary>Gets the number of packets in the query.</summary>
    public BigInteger Cardinality => _coverage.Cardinality;

    /// <summary>
    /// Returns the cell that contains <paramref name="point"/>, or <see langword="null"/> when the point is outside the query.
    /// The point must still lie in the chain's closed world.
    /// </summary>
    public PolicyDiffCell<TAddress>? Classify(PacketPoint<TAddress> point)
    {
        _layout.ValidatePoint(point);
        PolicyDiffCell<TAddress>? found = null;
        foreach (PolicyDiffCell<TAddress> cell in _cells)
        {
            if (!cell.Region.Contains(point))
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidOperationException("Policy diff cells overlap.");
            }

            found = cell;
        }

        return found;
    }

    /// <summary>Returns the cells in the <paramref name="kind"/> quadrant, still carrying both provenances.</summary>
    public IReadOnlyList<PolicyDiffCell<TAddress>> CellsFor(PolicyDiffKind kind)
    {
        EnsureKind(kind);
        List<PolicyDiffCell<TAddress>> cells = [];
        foreach (PolicyDiffCell<TAddress> cell in _cells)
        {
            if (cell.Difference == kind)
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    /// <summary>
    /// Returns the packets in the <paramref name="kind"/> quadrant.
    /// The result is a set: packets in other quadrants are absent, and provenance is not preserved.
    /// </summary>
    public PacketSpace<TAddress> SpaceFor(PolicyDiffKind kind)
    {
        EnsureKind(kind);
        return SpaceWhere(cell => cell.Difference == kind);
    }

    /// <summary>
    /// Returns the packets whose effective decision differs, whether or not responsibility also shifts.
    /// The set may overlap <see cref="ResponsibilityShifts"/>.
    /// </summary>
    public PacketSpace<TAddress> DecisionChanges() => SpaceWhere(static cell => cell.DecisionChanged);

    /// <summary>
    /// Returns the packets whose semantic responsibility differs, whether or not the decision also changes.
    /// The set may overlap <see cref="DecisionChanges"/>. Family order alone does not qualify.
    /// </summary>
    public PacketSpace<TAddress> ResponsibilityShifts() => SpaceWhere(static cell => cell.ResponsibilityChanged);

    /// <summary>Returns the packets whose decision and semantic responsibility are both unchanged.</summary>
    public PacketSpace<TAddress> Unchanged() => SpaceFor(PolicyDiffKind.Unchanged);

    /// <summary>
    /// Returns the packets whose decision or semantic responsibility differs.
    /// The union of this set and <see cref="Unchanged"/> is the query, and the two sets do not overlap.
    /// </summary>
    public PacketSpace<TAddress> Changed() => SpaceWhere(static cell => cell.Difference != PolicyDiffKind.Unchanged);

    internal static PolicyDiff<TAddress> Create(
        PacketLayout<TAddress> layout,
        List<PolicyDiffCell<TAddress>> cells,
        ProductSpace coverage)
    {
        List<PolicyDiffCell<TAddress>> coalesced = Coalesce(layout, cells);
        EnsureCover(coalesced, coverage);
        return new PolicyDiff<TAddress>(layout, coverage, [.. coalesced]);
    }

    private PacketSpace<TAddress> SpaceWhere(Func<PolicyDiffCell<TAddress>, bool> include)
    {
        List<ProductRegion> regions = [];
        foreach (PolicyDiffCell<TAddress> cell in _cells)
        {
            if (include(cell))
            {
                regions.Add(_layout.FromPacket(cell.Region));
            }
        }

        // Filtering a disjoint comparison partition preserves disjointness.
        return new PacketSpace<TAddress>(_layout, ProductSpace.FromDisjointRegions(regions));
    }

    private static List<PolicyDiffCell<TAddress>> Coalesce(PacketLayout<TAddress> layout, List<PolicyDiffCell<TAddress>> cells)
    {
        Dictionary<CellKey, List<ProductRegion>> groups = [];
        foreach (PolicyDiffCell<TAddress> cell in cells)
        {
            CellKey key = new(cell.BaselineDecision, cell.CandidateDecision, cell.BaselineProvenance, cell.CandidateProvenance);
            if (!groups.TryGetValue(key, out List<ProductRegion>? group))
            {
                group = [];
                groups.Add(key, group);
            }

            group.Add(layout.FromPacket(cell.Region));
        }

        List<PolicyDiffCell<TAddress>> merged = [];
        foreach ((CellKey key, List<ProductRegion> group) in groups)
        {
            // The input cells are pairwise intersections of two validated disjoint partitions.
            ProductSpace space = ProductSpace.FromDisjointRegions(group);
            foreach (ProductRegion region in space.Regions)
            {
                merged.Add(new PolicyDiffCell<TAddress>(
                    layout.ToPacket(region),
                    key.BaselineDecision,
                    key.CandidateDecision,
                    key.BaselineProvenance,
                    key.CandidateProvenance));
            }
        }

        merged.Sort(static (left, right) => left.Region.CompareTo(right.Region));
        return merged;
    }

    // The overlay intersects two already-validated disjoint partitions of the same query.
    // Pairwise intersections lie inside the query and are disjoint by construction; coalescing
    // preserves their union. Rechecking all cell pairs or subtracting the query from every
    // output cell would duplicate the validated input partitions' work at cross-product scale.
    private static void EnsureCover(List<PolicyDiffCell<TAddress>> cells, ProductSpace coverage)
    {
        BigInteger sum = BigInteger.Zero;
        for (int index = 0; index < cells.Count; index++)
        {
            PolicyDiffCell<TAddress> cell = cells[index];
            if (cell.Region.Cardinality == BigInteger.Zero)
            {
                throw new InvalidOperationException("Policy diff contains an empty cell.");
            }

            sum += cell.Region.Cardinality;
        }

        if (sum != coverage.Cardinality)
        {
            throw new InvalidOperationException("Policy diff does not cover the queried packet space.");
        }
    }

    private static void EnsureKind(PolicyDiffKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported policy diff kind.");
        }
    }

    private readonly record struct CellKey(
        PolicyDecision BaselineDecision,
        PolicyDecision CandidateDecision,
        DecisionProvenance BaselineProvenance,
        DecisionProvenance CandidateProvenance);
}
