using System.Collections.Immutable;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class UfwArgumentCountReinsertionCostProviderTests
{
    [TestMethod]
    public void GetReinsertionCost_UsesRenderedArgumentCount()
    {
        UfwRenderedRule renderedRule = new(ImmutableArray.Create("allow", "in", "proto", "tcp", "to", "any", "port", "22"), "allow in proto tcp to any port 22");

        int cost = new UfwArgumentCountReinsertionCostProvider().GetReinsertionCost(renderedRule);

        Assert.AreEqual(renderedRule.Arguments.Length, cost);
    }
}
