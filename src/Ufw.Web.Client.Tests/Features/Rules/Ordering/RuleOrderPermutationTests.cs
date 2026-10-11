using Ufw.Web.Client.Features.Rules.Ordering;

namespace Ufw.Web.Client.Tests.Features.Rules.Ordering;

[TestClass]
public sealed class RuleOrderPermutationTests
{
    [TestMethod]
    public void Create_ContainsEachBaselineOccurrenceAndDefensivelyCopiesInput()
    {
        int[] order = [2, 0, 1];

        RuleOrderPermutation permutation = RuleOrderPermutation.Create(order, 3);
        order[0] = 0;

        CollectionAssert.AreEqual(new[] { 2, 0, 1 }, permutation.Occurrences.ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<int>)permutation.Occurrences)[0] = 1);
    }

    [TestMethod]
    public void Create_RejectsIncompleteDuplicateAndOutOfRangeOrders()
    {
        Assert.ThrowsExactly<ArgumentException>(() => RuleOrderPermutation.Create([0, 1], 3));
        Assert.ThrowsExactly<ArgumentException>(() => RuleOrderPermutation.Create([0, 1, 1], 3));
        Assert.ThrowsExactly<ArgumentException>(() => RuleOrderPermutation.Create([-1, 0, 1], 3));
        Assert.ThrowsExactly<ArgumentException>(() => RuleOrderPermutation.Create([0, 1, 3], 3));
    }

    [TestMethod]
    public void Create_AcceptsEmptySnapshotOnlyWithEmptyPermutation()
    {
        Assert.IsEmpty(RuleOrderPermutation.Create([], 0).Occurrences);
        Assert.ThrowsExactly<ArgumentException>(() => RuleOrderPermutation.Create([0], 0));
    }
}
