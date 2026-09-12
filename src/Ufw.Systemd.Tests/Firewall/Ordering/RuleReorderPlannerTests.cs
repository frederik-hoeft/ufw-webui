using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class RuleReorderPlannerTests
{
    private readonly RuleReorderPlanner _planner = new();

    [TestMethod]
    public void Plan_IdenticalOrder_ProducesNoMoves()
    {
        int[] order = [0, 1, 2, 3];

        RuleReorderPlan plan = _planner.Plan(order, order, new HashSet<int>());

        Assert.HasCount(4, plan.UntouchedOccurrences);
        Assert.IsEmpty(plan.Moves);
    }

    [TestMethod]
    public void Plan_KnownPermutation_ProducesMinimumAnchoredMoves()
    {
        int[] current = [0, 1, 2, 3, 4];
        int[] desired = [0, 3, 1, 4, 2];

        RuleReorderPlan plan = _planner.Plan(current, desired, new HashSet<int>());
        int[] applied = Apply(current, plan.Moves);

        CollectionAssert.AreEqual(desired, applied);
        Assert.HasCount(2, plan.Moves);
        Assert.AreEqual(2, plan.Moves[0].OccurrenceId);
        Assert.IsNull(plan.Moves[0].BeforeOccurrenceId);
        Assert.AreEqual(4, plan.Moves[0].TargetIndex);
        Assert.AreEqual(1, plan.Moves[1].OccurrenceId);
        Assert.AreEqual(4, plan.Moves[1].BeforeOccurrenceId);
        Assert.AreEqual(2, plan.Moves[1].TargetIndex);
    }

    [TestMethod]
    public void Plan_ImmutableAnchorsRemainUntouchedWhileMovableRulesCrossThem()
    {
        int[] current = [0, 1, 2, 3, 4, 5];
        int[] desired = [4, 0, 1, 3, 5, 2];
        HashSet<int> immutable = [1, 3];

        RuleReorderPlan plan = _planner.Plan(current, desired, immutable);

        CollectionAssert.AreEqual(desired, Apply(current, plan.Moves));
        Assert.IsTrue(plan.UntouchedOccurrences.Contains(1));
        Assert.IsTrue(plan.UntouchedOccurrences.Contains(3));
        Assert.IsFalse(plan.Moves.Any(move => immutable.Contains(move.OccurrenceId)));
    }

    [TestMethod]
    public void Plan_ImmutableAnchorOrderChange_IsRejected()
    {
        int[] current = [0, 1, 2, 3];
        int[] desired = [0, 3, 2, 1];
        HashSet<int> immutable = [1, 3];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => _planner.Plan(current, desired, immutable));

        StringAssert.Contains(exception.Message, "immutable occurrence");
    }

    [TestMethod]
    public void Plan_InvalidPermutations_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => _planner.Plan([0, 0], [0, 1], new HashSet<int>()));
        Assert.Throws<ArgumentException>(() => _planner.Plan([0, 1], [0, 2], new HashSet<int>()));
        Assert.Throws<ArgumentException>(() => _planner.Plan([0, 1], [0, 1, 2], new HashSet<int>()));
        Assert.Throws<ArgumentException>(() => _planner.Plan([0, 1], [0, 1], new HashSet<int> { 2 }));
    }

    [TestMethod]
    public void Plan_AllPermutationsThroughSevenOccurrences_AreGloballyMinimalAndDeterministic()
    {
        for (int count = 0; count <= 7; count++)
        {
            int[] current = Enumerable.Range(0, count).ToArray();
            foreach (int[] desired in EnumeratePermutations(current))
            {
                RuleReorderPlan first = _planner.Plan(current, desired, new HashSet<int>());
                RuleReorderPlan second = _planner.Plan(current, desired, new HashSet<int>());

                CollectionAssert.AreEqual(desired, Apply(current, first.Moves));
                Assert.AreEqual(count - LongestCommonSubsequenceLength(current, desired), first.Moves.Count);
                CollectionAssert.AreEqual(first.Moves.ToArray(), second.Moves.ToArray());
            }
        }
    }

    [TestMethod]
    public void Plan_ImmutableSubsetsThroughFiveOccurrences_AreOptimalOrRejected()
    {
        for (int count = 0; count <= 5; count++)
        {
            int[] current = Enumerable.Range(0, count).ToArray();
            foreach (int[] desired in EnumeratePermutations(current))
            {
                for (int mask = 0; mask < (1 << count); mask++)
                {
                    HashSet<int> immutable = CreateSubset(count, mask);
                    int maximumUntouched = MaximumCommonSubsequenceContaining(current, desired, immutable);
                    if (maximumUntouched < 0)
                    {
                        Assert.Throws<InvalidOperationException>(() => _planner.Plan(current, desired, immutable));
                        continue;
                    }

                    RuleReorderPlan plan = _planner.Plan(current, desired, immutable);
                    CollectionAssert.AreEqual(desired, Apply(current, plan.Moves));
                    Assert.AreEqual(count - maximumUntouched, plan.Moves.Count);
                    Assert.IsFalse(plan.Moves.Any(move => immutable.Contains(move.OccurrenceId)));
                }
            }
        }
    }

    [TestMethod]
    public void Plan_KeepPrioritiesBreakEqualLengthLisTiesWithoutIncreasingMoveCount()
    {
        int[] current = [0, 1, 2, 3];
        int[] desired = [1, 0, 3, 2];
        Dictionary<int, int> priorities = new()
        {
            [1] = 10,
            [3] = 10,
        };

        RuleReorderPlan plan = _planner.Plan(current, desired, new HashSet<int>(), priorities);

        Assert.AreEqual(2, plan.Moves.Count);
        Assert.IsTrue(plan.UntouchedOccurrences.SetEquals([1, 3]));
        CollectionAssert.AreEqual(desired, Apply(current, plan.Moves));
    }

    private static int[] Apply(IReadOnlyList<int> current, IReadOnlyList<RuleReorderMove> moves)
    {
        List<int> result = [.. current];
        foreach (RuleReorderMove move in moves)
        {
            Assert.IsTrue(result.Remove(move.OccurrenceId));
            if (move.BeforeOccurrenceId.HasValue)
            {
                int anchorIndex = result.IndexOf(move.BeforeOccurrenceId.Value);
                Assert.IsGreaterThanOrEqualTo(0, anchorIndex);
                result.Insert(anchorIndex, move.OccurrenceId);
            }
            else
            {
                result.Add(move.OccurrenceId);
            }
        }

        return [.. result];
    }

    private static IEnumerable<int[]> EnumeratePermutations(IReadOnlyList<int> values)
    {
        int[] buffer = values.ToArray();
        return EnumeratePermutations(buffer, 0);
    }

    private static IEnumerable<int[]> EnumeratePermutations(int[] buffer, int index)
    {
        if (index == buffer.Length)
        {
            yield return buffer.ToArray();
            yield break;
        }

        for (int swapIndex = index; swapIndex < buffer.Length; swapIndex++)
        {
            (buffer[index], buffer[swapIndex]) = (buffer[swapIndex], buffer[index]);
            foreach (int[] permutation in EnumeratePermutations(buffer, index + 1))
            {
                yield return permutation;
            }

            (buffer[index], buffer[swapIndex]) = (buffer[swapIndex], buffer[index]);
        }
    }

    private static int LongestCommonSubsequenceLength(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        int[,] lengths = new int[left.Count + 1, right.Count + 1];
        for (int leftIndex = 1; leftIndex <= left.Count; leftIndex++)
        {
            for (int rightIndex = 1; rightIndex <= right.Count; rightIndex++)
            {
                lengths[leftIndex, rightIndex] = left[leftIndex - 1] == right[rightIndex - 1]
                    ? lengths[leftIndex - 1, rightIndex - 1] + 1
                    : Math.Max(lengths[leftIndex - 1, rightIndex], lengths[leftIndex, rightIndex - 1]);
            }
        }

        return lengths[left.Count, right.Count];
    }

    private static HashSet<int> CreateSubset(int count, int mask)
    {
        HashSet<int> result = [];
        for (int occurrenceId = 0; occurrenceId < count; occurrenceId++)
        {
            if ((mask & (1 << occurrenceId)) != 0)
            {
                result.Add(occurrenceId);
            }
        }

        return result;
    }

    private static int MaximumCommonSubsequenceContaining(
        IReadOnlyList<int> current,
        IReadOnlyList<int> desired,
        IReadOnlySet<int> required)
    {
        Dictionary<int, int> desiredPositions = desired
            .Select(static (occurrenceId, index) => (occurrenceId, index))
            .ToDictionary(static item => item.occurrenceId, static item => item.index);
        int best = -1;
        int subsetCount = 1 << current.Count;
        for (int mask = 0; mask < subsetCount; mask++)
        {
            List<int> selected = [];
            for (int index = 0; index < current.Count; index++)
            {
                if ((mask & (1 << index)) != 0)
                {
                    selected.Add(current[index]);
                }
            }

            if (!required.IsSubsetOf(selected))
            {
                continue;
            }

            bool increasing = true;
            for (int index = 1; index < selected.Count; index++)
            {
                if (desiredPositions[selected[index - 1]] >= desiredPositions[selected[index]])
                {
                    increasing = false;
                    break;
                }
            }

            if (increasing)
            {
                best = Math.Max(best, selected.Count);
            }
        }

        return best;
    }
}
