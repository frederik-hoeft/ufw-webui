using System.Numerics;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Non-overlapping cover of a queried packet space. Every packet in the query belongs to exactly one cell.
/// </summary>
public sealed class PolicyPartition<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
{
    private readonly PacketLayout<TAddress> _layout;
    private readonly ProductSpace _coverage;
    private readonly PolicyCell<TAddress>[] _cells;

    private PolicyPartition(PacketLayout<TAddress> layout, ProductSpace coverage, PolicyCell<TAddress>[] cells)
    {
        _layout = layout;
        _coverage = coverage;
        _cells = cells;
    }

    /// <summary>Gets the chain that was evaluated.</summary>
    public TrafficChain Chain => _layout.Profile.Chain;

    /// <summary>Gets the address family that was evaluated.</summary>
    public IpFamily Family => _layout.World.Family;

    /// <summary>Gets the cells in canonical region order.</summary>
    public IReadOnlyList<PolicyCell<TAddress>> Cells => _cells;

    /// <summary>Gets a value indicating whether the query contained no packets.</summary>
    public bool IsEmpty => _cells.Length == 0;

    /// <summary>Gets the number of packets in the query.</summary>
    public BigInteger Cardinality => _coverage.Cardinality;

    /// <summary>
    /// Returns the cell that contains <paramref name="point"/>, or <see langword="null"/> when the point is outside the query.
    /// The point must still lie in the chain's closed world.
    /// </summary>
    public PolicyCell<TAddress>? Decide(PacketPoint<TAddress> point)
    {
        _layout.ValidatePoint(point);
        PolicyCell<TAddress>? found = null;
        foreach (PolicyCell<TAddress> cell in _cells)
        {
            if (!cell.Region.Contains(point))
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidOperationException("Policy partition cells overlap.");
            }

            found = cell;
        }

        return found;
    }

    /// <summary>
    /// Intersects every cell with <paramref name="constraint"/>. Provenance is preserved.
    /// This does not re-run first match; the original evaluation already fixed the decision of every packet.
    /// </summary>
    public PolicyPartition<TAddress> Constrain(PolicyConstraint<TAddress> constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        ProductSpace cut = _layout.Materialize(constraint);
        List<PolicyCell<TAddress>> cells = [];
        foreach (PolicyCell<TAddress> cell in _cells)
        {
            ProductSpace hit = ProductSpace.FromRegions([_layout.FromPacket(cell.Region)]).Intersect(cut);
            foreach (ProductRegion region in hit.Regions)
            {
                cells.Add(new PolicyCell<TAddress>(_layout.ToPacket(region), cell.Decision, cell.Provenance));
            }
        }

        return Create(_layout, cells, _coverage.Intersect(cut));
    }

    /// <summary>Returns the cells whose decision is <paramref name="decision"/>, still carrying provenance.</summary>
    public IReadOnlyList<PolicyCell<TAddress>> CellsFor(PolicyDecision decision)
    {
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unsupported policy decision.");
        }

        List<PolicyCell<TAddress>> cells = [];
        foreach (PolicyCell<TAddress> cell in _cells)
        {
            if (cell.Decision == decision)
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    /// <summary>
    /// Returns the packets whose decision is <paramref name="decision"/>, coalesced across rules.
    /// The result is a set, not a partition: packets with other decisions are absent.
    /// </summary>
    public PacketSpace<TAddress> SpaceFor(PolicyDecision decision)
    {
        List<PacketRegion<TAddress>> regions = [];
        foreach (PolicyCell<TAddress> cell in CellsFor(decision))
        {
            regions.Add(cell.Region);
        }

        return FromRegions(regions);
    }

    /// <summary>Returns the packets decided by <paramref name="provenance"/>.</summary>
    public PacketSpace<TAddress> SpaceFor(DecisionProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        List<PacketRegion<TAddress>> regions = [];
        foreach (PolicyCell<TAddress> cell in _cells)
        {
            if (cell.Provenance.Equals(provenance))
            {
                regions.Add(cell.Region);
            }
        }

        return FromRegions(regions);
    }

    /// <summary>Gets the rules that decided at least one packet, in family order.</summary>
    public IReadOnlyList<DecisionProvenance.RuleMatch> ResponsibleRules
    {
        get
        {
            List<DecisionProvenance.RuleMatch> rules = [];
            foreach (PolicyCell<TAddress> cell in _cells)
            {
                if (cell.Provenance is DecisionProvenance.RuleMatch match && !rules.Contains(match))
                {
                    rules.Add(match);
                }
            }

            rules.Sort(static (left, right) => left.FamilyOrder.CompareTo(right.FamilyOrder));
            return rules;
        }
    }

    internal static PolicyPartition<TAddress> Create(
        PacketLayout<TAddress> layout,
        List<PolicyCell<TAddress>> cells,
        ProductSpace coverage)
    {
        List<PolicyCell<TAddress>> coalesced = Coalesce(layout, cells);
        EnsureCover(layout, coalesced, coverage);
        return new PolicyPartition<TAddress>(layout, coverage, [.. coalesced]);
    }

    private PacketSpace<TAddress> FromRegions(List<PacketRegion<TAddress>> regions) =>
        new(_layout, ProductSpace.FromRegions(regions.Select(_layout.FromPacket)));

    private static List<PolicyCell<TAddress>> Coalesce(PacketLayout<TAddress> layout, List<PolicyCell<TAddress>> cells)
    {
        Dictionary<CellKey, List<ProductRegion>> groups = new();
        foreach (PolicyCell<TAddress> cell in cells)
        {
            CellKey key = new(cell.Decision, cell.Provenance);
            if (!groups.TryGetValue(key, out List<ProductRegion>? group))
            {
                group = [];
                groups.Add(key, group);
            }

            group.Add(layout.FromPacket(cell.Region));
        }

        List<PolicyCell<TAddress>> merged = [];
        foreach ((CellKey key, List<ProductRegion> group) in groups)
        {
            ProductSpace space = ProductSpace.FromRegions(group);
            foreach (ProductRegion region in space.Regions)
            {
                merged.Add(new PolicyCell<TAddress>(layout.ToPacket(region), key.Decision, key.Provenance));
            }
        }

        merged.Sort(static (left, right) => left.Region.CompareTo(right.Region));
        return merged;
    }

    private static void EnsureCover(PacketLayout<TAddress> layout, List<PolicyCell<TAddress>> cells, ProductSpace coverage)
    {
        BigInteger sum = BigInteger.Zero;
        for (int index = 0; index < cells.Count; index++)
        {
            PolicyCell<TAddress> cell = cells[index];
            if (cell.Region.Cardinality == BigInteger.Zero)
            {
                throw new InvalidOperationException("Policy partition contains an empty cell.");
            }

            for (int other = index + 1; other < cells.Count; other++)
            {
                if (cell.Region.Overlaps(cells[other].Region))
                {
                    throw new InvalidOperationException("Policy partition cells overlap.");
                }
            }

            ProductSpace outside = ProductSpace.FromRegions([layout.FromPacket(cell.Region)]).Except(coverage);
            if (!outside.IsEmpty)
            {
                throw new InvalidOperationException("A policy cell extends outside the queried packet space.");
            }

            sum += cell.Region.Cardinality;
        }

        if (sum != coverage.Cardinality)
        {
            throw new InvalidOperationException("Policy partition does not cover the queried packet space.");
        }
    }

    private readonly struct CellKey(PolicyDecision decision, DecisionProvenance provenance) : IEquatable<CellKey>
    {
        public PolicyDecision Decision { get; } = decision;

        public DecisionProvenance Provenance { get; } = provenance;

        public bool Equals(CellKey other) => Decision == other.Decision && Provenance.Equals(other.Provenance);

        public override bool Equals(object? obj) => obj is CellKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Decision, Provenance);
    }
}
