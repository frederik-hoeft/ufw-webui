using System.Numerics;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain.Algebra;

[TestClass]
public sealed class ProductSpaceTests
{
    [TestMethod]
    public void Except_UsesDisjointDecompositionAndPreservesCardinality()
    {
        ProductRegion whole = Rectangle(0, 9, 0, 9, 0, 4);
        ProductRegion cut = Rectangle(2, 4, 0, 9, 1, 1);
        ProductSpace space = ProductSpace.FromRegions([whole]);
        ProductSpace inside = space.Intersect(cut);
        ProductSpace outside = space.Except(cut);

        Assert.AreEqual(whole.Cardinality, inside.Cardinality + outside.Cardinality);
        Assert.IsFalse(outside.Overlaps(cut));
        Assert.AreEqual(inside.Cardinality, cut.Cardinality);
        Assert.IsTrue(outside.Regions.Count >= 1);
    }

    [TestMethod]
    public void Coalesce_MergesRectanglesThatDifferInOneAxis()
    {
        ProductRegion left = Rectangle(0, 1, 0, 0, 0, 0);
        ProductRegion right = Rectangle(2, 4, 0, 0, 0, 0);
        ProductSpace space = ProductSpace.FromRegions([left, right]);

        Assert.AreEqual(1, space.Regions.Count);
        Assert.AreEqual(left.Cardinality + right.Cardinality, space.Cardinality);
        IntervalSet<ushort> merged = (IntervalSet<ushort>)space.Regions[0].Axis(0);
        Assert.AreEqual(IntervalSet<ushort>.Between(0, 4), merged);
    }

    [TestMethod]
    public void SmallGrid_ExceptAgreesWithPointEnumeration()
    {
        const int limit = 12;
        ProductRegion whole = Rectangle(0, limit, 0, limit, 0, limit);
        ProductRegion cut = Rectangle(3, 8, 0, 4, 10, limit);
        ProductSpace outside = ProductSpace.FromRegions([whole]).Except(cut);
        BigInteger seen = BigInteger.Zero;
        for (ushort x = 0; x <= limit; x++)
        {
            for (ushort y = 0; y <= limit; y++)
            {
                for (ushort z = 0; z <= limit; z++)
                {
                    bool inCut = x is >= 3 and <= 8 && y <= 4 && z >= 10;
                    bool inOutside = Contains(outside, x, y, z);
                    Assert.AreNotEqual(inCut, inOutside);
                    if (inOutside)
                    {
                        seen++;
                    }
                }
            }
        }

        Assert.AreEqual(outside.Cardinality, seen);
        Assert.AreEqual(whole.Cardinality, outside.Cardinality + cut.Cardinality);
    }

    private static ProductRegion Rectangle(ushort x0, ushort x1, ushort y0, ushort y1, ushort z0, ushort z1) => new(
    [
        IntervalSet<ushort>.Between(x0, x1),
        IntervalSet<ushort>.Between(y0, y1),
        IntervalSet<ushort>.Between(z0, z1),
    ]);

    private static bool Contains(ProductSpace space, ushort x, ushort y, ushort z)
    {
        foreach (ProductRegion region in space.Regions)
        {
            if (((IntervalSet<ushort>)region.Axis(0)).Contains(x)
                && ((IntervalSet<ushort>)region.Axis(1)).Contains(y)
                && ((IntervalSet<ushort>)region.Axis(2)).Contains(z))
            {
                return true;
            }
        }

        return false;
    }
}
