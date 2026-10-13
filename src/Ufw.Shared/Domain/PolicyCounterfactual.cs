namespace Ufw.Shared.Domain;

/// <summary>
/// Semantic impact of a hypothetical edit, answered by comparing the original world with a derived candidate.
/// </summary>
/// <remarks>
/// The original world is never modified. Removal is the first such edit: later edits can build a different candidate
/// and call <see cref="PolicyDiffer.Compare{TAddress}"/> rather than growing a second evaluator.
/// </remarks>
public static class PolicyCounterfactual
{
    /// <summary>
    /// Returns the diff of removing <paramref name="occurrence"/> from <paramref name="world"/> under <paramref name="query"/>.
    /// </summary>
    /// <param name="world">The baseline world. It is not modified.</param>
    /// <param name="occurrence">The rule instance to remove. Identity alone is not selective when duplicates exist.</param>
    /// <param name="query">The packet space whose impact is required. The same query is applied before and after removal.</param>
    /// <returns>
    /// The comparison of <paramref name="world"/> with the world formed by dropping that occurrence.
    /// Baseline values are before removal. Candidate values are after removal.
    /// </returns>
    /// <remarks>
    /// Remaining rules keep their relative order and receive new family indexes. A later rule that still decides a packet
    /// is not a responsibility shift merely because its index changed. A later occurrence of the same rule identity that
    /// takes over the removed occurrence is not a responsibility shift either. A rule on a chain the query does not
    /// evaluate leaves that query unchanged.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="occurrence"/> does not belong to <paramref name="world"/>,
    /// or when the query address family does not match the world.
    /// </exception>
    public static PolicyDiff<TAddress> ImpactOfRemoving<TAddress>(
        PolicyWorld<TAddress> world,
        PolicyRule<TAddress> occurrence,
        PolicyQuery<TAddress> query)
        where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(query);
        PolicyWorld<TAddress> candidate = world.WithoutOccurrence(occurrence);
        return PolicyDiffer.Compare(world, candidate, query);
    }
}
