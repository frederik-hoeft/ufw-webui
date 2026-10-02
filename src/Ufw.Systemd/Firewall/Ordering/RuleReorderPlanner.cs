using System.Collections.Frozen;

namespace Ufw.Systemd.Firewall.Ordering;

/// <summary>
/// Produces a minimum-cardinality remove/reinsert plan over snapshot-local occurrence IDs.
/// Among plans with the same move count, prefers leaving occurrences with the greater total reinsertion cost untouched.
/// </summary>
internal sealed class RuleReorderPlanner : IRuleReorderPlanner
{
    public RuleReorderPlan Plan(
        IReadOnlyList<int> currentOrder,
        IReadOnlyList<int> desiredOrder,
        IReadOnlySet<int> immutableOccurrences,
        IReadOnlyDictionary<int, int>? reinsertionCosts = null)
    {
        ArgumentNullException.ThrowIfNull(currentOrder);
        ArgumentNullException.ThrowIfNull(desiredOrder);
        ArgumentNullException.ThrowIfNull(immutableOccurrences);
        reinsertionCosts ??= FrozenDictionary<int, int>.Empty;

        ValidatePermutation(currentOrder, nameof(currentOrder));
        ValidatePermutation(desiredOrder, nameof(desiredOrder));
        if (currentOrder.Count != desiredOrder.Count)
        {
            throw new ArgumentException("Current and desired orders must contain the same occurrence set.", nameof(desiredOrder));
        }

        int occurrenceCount = currentOrder.Count;
        foreach (int occurrenceId in immutableOccurrences)
        {
            if (occurrenceId < 0 || occurrenceId >= occurrenceCount)
            {
                throw new ArgumentException("Immutable occurrence IDs must belong to the baseline occurrence set.", nameof(immutableOccurrences));
            }
        }

        int[] desiredPositions = CreatePositionMap(desiredOrder);
        ValidateReinsertionCosts(reinsertionCosts, occurrenceCount);
        HashSet<int> untouched = SelectUntouchedOccurrences(currentOrder, desiredPositions, immutableOccurrences, reinsertionCosts);
        List<RuleReorderMove> moves = CreateMoves(desiredOrder, untouched);
        return new RuleReorderPlan(untouched, moves);
    }

    private static HashSet<int> SelectUntouchedOccurrences(
        IReadOnlyList<int> currentOrder,
        IReadOnlyList<int> desiredPositions,
        IReadOnlySet<int> immutableOccurrences,
        IReadOnlyDictionary<int, int> reinsertionCosts)
    {
        List<ImmutableAnchor> anchors = [];
        for (int currentIndex = 0; currentIndex < currentOrder.Count; currentIndex++)
        {
            int occurrenceId = currentOrder[currentIndex];
            if (immutableOccurrences.Contains(occurrenceId))
            {
                anchors.Add(new ImmutableAnchor(occurrenceId, currentIndex, desiredPositions[occurrenceId]));
            }
        }

        for (int index = 1; index < anchors.Count; index++)
        {
            if (anchors[index - 1].DesiredIndex >= anchors[index].DesiredIndex)
            {
                throw new InvalidOperationException("Desired ordering would require moving an immutable occurrence.");
            }
        }

        HashSet<int> untouched = new(anchors.Select(static anchor => anchor.OccurrenceId));
        int previousCurrentIndex = -1;
        int previousDesiredIndex = -1;
        for (int anchorIndex = 0; anchorIndex <= anchors.Count; anchorIndex++)
        {
            int nextCurrentIndex = anchorIndex < anchors.Count
                ? anchors[anchorIndex].CurrentIndex
                : currentOrder.Count;
            int nextDesiredIndex = anchorIndex < anchors.Count
                ? anchors[anchorIndex].DesiredIndex
                : currentOrder.Count;

            AddLongestIncreasingSubsequence(currentOrder, desiredPositions, previousCurrentIndex + 1, nextCurrentIndex, previousDesiredIndex, nextDesiredIndex, reinsertionCosts, untouched);

            previousCurrentIndex = nextCurrentIndex;
            previousDesiredIndex = nextDesiredIndex;
        }

        return untouched;
    }

    private static void AddLongestIncreasingSubsequence(
        IReadOnlyList<int> currentOrder,
        IReadOnlyList<int> desiredPositions,
        int startIndex,
        int endIndex,
        int lowerDesiredExclusive,
        int upperDesiredExclusive,
        IReadOnlyDictionary<int, int> reinsertionCosts,
        HashSet<int> untouched)
    {
        List<int> candidates = [];
        for (int currentIndex = startIndex; currentIndex < endIndex; currentIndex++)
        {
            int occurrenceId = currentOrder[currentIndex];
            int desiredIndex = desiredPositions[occurrenceId];
            if (desiredIndex > lowerDesiredExclusive && desiredIndex < upperDesiredExclusive)
            {
                candidates.Add(occurrenceId);
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        int[] predecessors = new int[candidates.Count];
        Array.Fill(predecessors, -1);
        WeightedLisFenwick fenwick = new(upperDesiredExclusive - lowerDesiredExclusive - 1);

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            int occurrenceId = candidates[candidateIndex];
            int desiredIndex = desiredPositions[occurrenceId];
            int localDesiredPosition = desiredIndex - lowerDesiredExclusive;
            WeightedSequence predecessor = fenwick.Query(localDesiredPosition - 1);
            predecessors[candidateIndex] = predecessor.CandidateIndex;
            int reinsertionCost = reinsertionCosts.TryGetValue(occurrenceId, out int cost) ? cost : 0;
            WeightedSequence candidate = new(predecessor.Length + 1, predecessor.TotalReinsertionCost + reinsertionCost, candidateIndex);
            fenwick.Update(localDesiredPosition, candidate);
        }

        int selectedIndex = fenwick.Query(fenwick.Size).CandidateIndex;
        while (selectedIndex >= 0)
        {
            untouched.Add(candidates[selectedIndex]);
            selectedIndex = predecessors[selectedIndex];
        }
    }

    private static List<RuleReorderMove> CreateMoves(IReadOnlyList<int> desiredOrder, IReadOnlySet<int> untouched)
    {
        List<RuleReorderMove> moves = [];
        for (int targetIndex = desiredOrder.Count - 1; targetIndex >= 0; targetIndex--)
        {
            int occurrenceId = desiredOrder[targetIndex];
            if (untouched.Contains(occurrenceId))
            {
                continue;
            }

            int? beforeOccurrenceId = targetIndex + 1 < desiredOrder.Count
                ? desiredOrder[targetIndex + 1]
                : null;
            moves.Add(new RuleReorderMove(occurrenceId, targetIndex, beforeOccurrenceId));
        }

        return moves;
    }

    private static int[] CreatePositionMap(IReadOnlyList<int> order)
    {
        int[] positions = new int[order.Count];
        for (int index = 0; index < order.Count; index++)
        {
            positions[order[index]] = index;
        }

        return positions;
    }

    private static void ValidatePermutation(IReadOnlyList<int> order, string parameterName)
    {
        bool[] seen = new bool[order.Count];
        for (int index = 0; index < order.Count; index++)
        {
            int occurrenceId = order[index];
            if (occurrenceId < 0 || occurrenceId >= order.Count || seen[occurrenceId])
            {
                throw new ArgumentException("Order must contain every zero-based baseline occurrence ID exactly once.", parameterName);
            }

            seen[occurrenceId] = true;
        }
    }

    private static void ValidateReinsertionCosts(IReadOnlyDictionary<int, int> reinsertionCosts, int occurrenceCount)
    {
        foreach ((int occurrenceId, int cost) in reinsertionCosts)
        {
            if (occurrenceId < 0 || occurrenceId >= occurrenceCount)
            {
                throw new ArgumentException("Reinsertion-cost occurrence IDs must belong to the baseline occurrence set.", nameof(reinsertionCosts));
            }
            if (cost < 0)
            {
                throw new ArgumentException("Reinsertion costs cannot be negative.", nameof(reinsertionCosts));
            }
        }
    }

    private sealed class WeightedLisFenwick
    {
        private readonly WeightedSequence[] _tree;

        public WeightedLisFenwick(int size)
        {
            Size = size;
            _tree = new WeightedSequence[size + 1];
            Array.Fill(_tree, WeightedSequence.Empty);
        }

        public int Size { get; }

        public WeightedSequence Query(int position)
        {
            WeightedSequence best = WeightedSequence.Empty;
            for (int index = position; index > 0; index -= index & -index)
            {
                best = WeightedSequence.Better(best, _tree[index]);
            }
            return best;
        }

        public void Update(int position, WeightedSequence value)
        {
            for (int index = position; index <= Size; index += index & -index)
            {
                _tree[index] = WeightedSequence.Better(_tree[index], value);
            }
        }
    }

    private readonly record struct WeightedSequence(int Length, long TotalReinsertionCost, int CandidateIndex)
    {
        public static WeightedSequence Empty { get; } = new(0, 0, -1);

        public static WeightedSequence Better(WeightedSequence left, WeightedSequence right)
        {
            if (left.Length != right.Length)
            {
                return left.Length > right.Length ? left : right;
            }
            if (left.TotalReinsertionCost != right.TotalReinsertionCost)
            {
                return left.TotalReinsertionCost > right.TotalReinsertionCost ? left : right;
            }
            if (left.CandidateIndex < 0)
            {
                return right;
            }
            if (right.CandidateIndex < 0)
            {
                return left;
            }
            return left.CandidateIndex >= right.CandidateIndex ? left : right;
        }
    }

    private readonly record struct ImmutableAnchor(int OccurrenceId, int CurrentIndex, int DesiredIndex);

}
