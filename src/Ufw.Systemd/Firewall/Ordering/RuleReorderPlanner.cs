namespace Ufw.Systemd.Firewall.Ordering;

/// <summary>
/// Produces a minimum-cardinality remove/reinsert plan over snapshot-local occurrence IDs.
/// </summary>
internal sealed class RuleReorderPlanner : IRuleReorderPlanner
{
    public RuleReorderPlan Plan(
        IReadOnlyList<int> currentOrder,
        IReadOnlyList<int> desiredOrder,
        IReadOnlySet<int> immutableOccurrences)
    {
        ArgumentNullException.ThrowIfNull(currentOrder);
        ArgumentNullException.ThrowIfNull(desiredOrder);
        ArgumentNullException.ThrowIfNull(immutableOccurrences);

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
        HashSet<int> untouched = SelectUntouchedOccurrences(currentOrder, desiredPositions, immutableOccurrences);
        List<RuleReorderMove> moves = CreateMoves(desiredOrder, untouched);
        return new RuleReorderPlan(untouched, moves);
    }

    private static HashSet<int> SelectUntouchedOccurrences(
        IReadOnlyList<int> currentOrder,
        IReadOnlyList<int> desiredPositions,
        IReadOnlySet<int> immutableOccurrences)
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

            AddLongestIncreasingSubsequence(
                currentOrder,
                desiredPositions,
                previousCurrentIndex + 1,
                nextCurrentIndex,
                previousDesiredIndex,
                nextDesiredIndex,
                untouched);

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

        int[] tailDesiredPositions = new int[candidates.Count];
        int[] tailCandidateIndices = new int[candidates.Count];
        int[] predecessors = new int[candidates.Count];
        Array.Fill(predecessors, -1);
        int length = 0;

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            int desiredIndex = desiredPositions[candidates[candidateIndex]];
            int insertionIndex = LowerBound(tailDesiredPositions, length, desiredIndex);
            tailDesiredPositions[insertionIndex] = desiredIndex;
            tailCandidateIndices[insertionIndex] = candidateIndex;
            if (insertionIndex > 0)
            {
                predecessors[candidateIndex] = tailCandidateIndices[insertionIndex - 1];
            }

            if (insertionIndex == length)
            {
                length++;
            }
        }

        int selectedIndex = tailCandidateIndices[length - 1];
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

    private static int LowerBound(IReadOnlyList<int> values, int count, int target)
    {
        int lower = 0;
        int upper = count;
        while (lower < upper)
        {
            int middle = lower + ((upper - lower) / 2);
            if (values[middle] < target)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        return lower;
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

    private readonly record struct ImmutableAnchor(int OccurrenceId, int CurrentIndex, int DesiredIndex);

}
