using Ufw.Client.Rules;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Tests.Rules;

[TestClass]
public sealed class RuleDropTargetProjectionTests
{
    [TestMethod]
    public void Constructor_DownwardMovePlacesIndicatorAfterTarget()
    {
        RuleDropTargetProjection target = new(Row(1), Row(2));

        Assert.AreEqual(RuleDropIndicatorEdge.After, target.IndicatorEdge);
        Assert.AreEqual(2, target.Row.FamilyPosition);
    }

    [TestMethod]
    public void Constructor_UpwardMovePlacesIndicatorBeforeTarget()
    {
        RuleDropTargetProjection target = new(Row(3), Row(2));

        Assert.AreEqual(RuleDropIndicatorEdge.Before, target.IndicatorEdge);
        Assert.AreEqual(2, target.Row.FamilyPosition);
    }

    [TestMethod]
    public void Constructor_DifferentAddressFamiliesAreRejected()
    {
        RuleRowProjection source = Row(1, FirewallAddressFamily.IPv4);
        RuleRowProjection target = Row(2, FirewallAddressFamily.IPv6);

        Assert.ThrowsExactly<ArgumentException>(() => new RuleDropTargetProjection(source, target));
    }

    private static RuleRowProjection Row(int familyPosition, FirewallAddressFamily family = FirewallAddressFamily.IPv4) => new(
        new ListedFirewallRule(),
        family,
        familyPosition - 1,
        familyPosition,
        3,
        CanOrder: true,
        CanMutate: true,
        PositionChange: null);
}
