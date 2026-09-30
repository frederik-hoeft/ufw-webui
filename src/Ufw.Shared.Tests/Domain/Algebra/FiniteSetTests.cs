using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain.Algebra;

[TestClass]
public sealed class FiniteSetTests
{
    [TestMethod]
    public void From_SortsAndDropsDuplicates()
    {
        FiniteSet<string> set = FiniteSet<string>.From(["udp", "tcp", "tcp"]);

        Assert.AreEqual(2, set.Cardinality);
        Assert.AreEqual("tcp", set.Values[0]);
        Assert.AreEqual("udp", set.Values[1]);
        Assert.IsTrue(set.Contains("tcp"));
        Assert.IsFalse(set.Contains("icmp"));
    }

    [TestMethod]
    public void Operations_ObeySetLaws()
    {
        FiniteSet<string> left = FiniteSet<string>.Of("a", "b", "c");
        FiniteSet<string> right = FiniteSet<string>.Of("b", "d");

        Assert.AreEqual(FiniteSet<string>.Of("a", "b", "c", "d"), left.Union(right));
        Assert.AreEqual(FiniteSet<string>.Of("b"), left.Intersect(right));
        Assert.AreEqual(FiniteSet<string>.Of("a", "c"), left.Except(right));
        Assert.IsTrue(left.Overlaps(right));
        Assert.IsTrue(left.IsSupersetOf(FiniteSet<string>.Of("a")));
        Assert.IsFalse(left.IsSupersetOf(right));
        Assert.AreEqual(left.Cardinality, left.Intersect(right).Cardinality + left.Except(right).Cardinality);
        Assert.AreEqual(FiniteSet<string>.Empty, left.Except(left));
    }
}
