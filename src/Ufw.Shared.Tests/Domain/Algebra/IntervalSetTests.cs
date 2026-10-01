using System.Numerics;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain.Algebra;

[TestClass]
public sealed class IntervalSetTests
{
    [TestMethod]
    public void Of_MergesOverlapAdjacencyAndDuplicates()
    {
        IntervalSet<uint> set = IntervalSet<uint>.Of(
        [
            new Interval<uint>(5, 7),
            new Interval<uint>(1, 3),
            new Interval<uint>(8, 8),
            new Interval<uint>(2, 4),
        ]);

        Assert.AreEqual(1, set.Intervals.Count);
        Assert.AreEqual(new Interval<uint>(1, 8), set.Intervals[0]);
        Assert.AreEqual(new BigInteger(8), set.Cardinality);
    }

    [TestMethod]
    public void Except_SplitsAroundAMiddleCut()
    {
        IntervalSet<uint> value = IntervalSet<uint>.Between(0, 10).Except(IntervalSet<uint>.Between(3, 5));

        Assert.AreEqual(2, value.Intervals.Count);
        Assert.AreEqual(new Interval<uint>(0, 2), value.Intervals[0]);
        Assert.AreEqual(new Interval<uint>(6, 10), value.Intervals[1]);
        Assert.IsFalse(value.Contains(3));
        Assert.IsTrue(value.Contains(2));
        Assert.IsTrue(value.Contains(6));
    }

    [TestMethod]
    public void Operations_PreserveCardinalityAtTheEndsOfTheIntegerDomain()
    {
        IntervalSet<uint> universe = IntervalSet<uint>.Between(0, uint.MaxValue);
        IntervalSet<uint> high = IntervalSet<uint>.Singleton(uint.MaxValue);
        IntervalSet<uint> low = IntervalSet<uint>.Singleton(0);
        IntervalSet<uint> removed = universe.Except(high).Except(low);

        Assert.AreEqual(new BigInteger(uint.MaxValue) + 1, universe.Cardinality);
        Assert.AreEqual(universe.Cardinality - 2, removed.Cardinality);
        Assert.IsFalse(removed.Contains(0));
        Assert.IsFalse(removed.Contains(uint.MaxValue));
        Assert.IsTrue(removed.Contains(1));
        Assert.IsTrue(removed.Contains(uint.MaxValue - 1));
        Assert.AreEqual(universe, low.Union(removed).Union(high));
    }

    [TestMethod]
    public void AdjacentMaximumValues_CoalesceWithoutOverflow()
    {
        IntervalSet<ushort> set = IntervalSet<ushort>.Singleton(ushort.MaxValue - 1).Union(IntervalSet<ushort>.Singleton(ushort.MaxValue));

        Assert.AreEqual(IntervalSet<ushort>.Between(ushort.MaxValue - 1, ushort.MaxValue), set);
    }

    [TestMethod]
    public void RandomOperations_ObeySetLaws()
    {
        Random random = new(20260328);
        IntervalSet<uint> universe = IntervalSet<uint>.Between(0, 400);
        for (int trial = 0; trial < 200; trial++)
        {
            IntervalSet<uint> left = RandomSet(random);
            IntervalSet<uint> right = RandomSet(random);

            Assert.AreEqual(left.Union(right), right.Union(left));
            Assert.AreEqual(left.Intersect(right), right.Intersect(left));
            Assert.AreEqual(left, left.Union(left));
            Assert.AreEqual(left, left.Intersect(left));
            Assert.AreEqual(IntervalSet<uint>.Empty, left.Except(left));
            Assert.AreEqual(IntervalSet<uint>.Empty, left.Intersect(IntervalSet<uint>.Empty));
            Assert.AreEqual(IntervalSet<uint>.Empty, left.Except(left).Intersect(left));
            Assert.AreEqual(left, left.Intersect(right).Union(left.Except(right)));
            Assert.AreEqual(left.Cardinality, left.Intersect(right).Cardinality + left.Except(right).Cardinality);
            Assert.IsTrue(universe.IsSupersetOf(left));
            Assert.IsFalse(left.Except(right).Overlaps(right));
        }
    }

    private static IntervalSet<uint> RandomSet(Random random)
    {
        int count = random.Next(0, 5);
        List<Interval<uint>> intervals = [];
        for (int index = 0; index < count; index++)
        {
            uint start = (uint)random.Next(0, 401);
            uint end = (uint)random.Next((int)start, 401);
            intervals.Add(new Interval<uint>(start, end));
        }

        return IntervalSet<uint>.Of(intervals);
    }
}
