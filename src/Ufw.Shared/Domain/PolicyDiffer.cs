using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Compares two closed policy worlds under one shared query.
/// </summary>
/// <remarks>
/// Each world is evaluated with ordinary first-match semantics. The diff is the overlay of those partitions,
/// not a third walk of the rules. Worlds whose packet universes differ are rejected: a smaller common universe
/// would change the meaning of unconstrained rules and of <c>any</c>.
/// </remarks>
public static class PolicyDiffer
{
    /// <summary>
    /// Partitions <paramref name="query"/> into the regions where <paramref name="baseline"/> and <paramref name="candidate"/> differ.
    /// </summary>
    /// <param name="baseline">The reference world. Its decision and provenance are the baseline side of each cell.</param>
    /// <param name="candidate">The world compared against <paramref name="baseline"/>.</param>
    /// <param name="query">The packet space to partition. The same query is applied to both worlds.</param>
    /// <returns>An exact partition of <paramref name="query"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a world or the query is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the closed packet universes differ, or when the query address family does not match the worlds.
    /// </exception>
    public static PolicyDiff<TAddress> Compare<TAddress>(
        PolicyWorld<TAddress> baseline,
        PolicyWorld<TAddress> candidate,
        PolicyQuery<TAddress> query)
        where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(query);
        if (!baseline.HasEquivalentPacketUniverse(candidate))
        {
            throw new ArgumentException(
                "Policy worlds can only be compared when their address family, address universe, protocol definitions, and interface universe are equivalent.",
                nameof(candidate));
        }

        if (query.Family != baseline.Family)
        {
            throw new ArgumentException("The query address family does not match the policy world.", nameof(query));
        }

        PolicyPartition<TAddress> baselinePartition = PolicyEvaluator.Evaluate(baseline, query);
        PolicyPartition<TAddress> candidatePartition = PolicyEvaluator.Evaluate(candidate, query);
        PacketLayout<TAddress> layout = PacketLayout<TAddress>.Create(baseline, ChainProfile.For(query.Chain));
        ProductSpace coverage = layout.Materialize(query.Constraint);
        if (baselinePartition.Cardinality != coverage.Cardinality || candidatePartition.Cardinality != coverage.Cardinality)
        {
            throw new InvalidOperationException("Compared policy evaluations do not cover the queried packet space.");
        }

        List<PolicyDiffCell<TAddress>> cells = Overlay(layout, baselinePartition, candidatePartition);
        return PolicyDiff<TAddress>.Create(layout, cells, coverage);
    }

    /// <summary>
    /// Intersects every baseline cell with every candidate cell.
    /// Each queried packet lies in exactly one cell of each partition, so the intersections are disjoint and cover the query.
    /// </summary>
    private static List<PolicyDiffCell<TAddress>> Overlay<TAddress>(
        PacketLayout<TAddress> layout,
        PolicyPartition<TAddress> baselinePartition,
        PolicyPartition<TAddress> candidatePartition)
        where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
    {
        List<PolicyDiffCell<TAddress>> cells = [];
        foreach (PolicyCell<TAddress> baselineCell in baselinePartition.Cells)
        {
            ProductRegion baselineRegion = layout.FromPacket(baselineCell.Region);
            foreach (PolicyCell<TAddress> candidateCell in candidatePartition.Cells)
            {
                ProductRegion hit = baselineRegion.Intersect(layout.FromPacket(candidateCell.Region));
                if (hit.IsEmpty)
                {
                    continue;
                }

                cells.Add(new PolicyDiffCell<TAddress>(
                    layout.ToPacket(hit),
                    baselineCell.Decision,
                    candidateCell.Decision,
                    baselineCell.Provenance,
                    candidateCell.Provenance));
            }
        }

        return cells;
    }
}
