using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Tests.Rules;

[TestClass]
public sealed class RuleFamilyProjectionTests
{
    [TestMethod]
    public void Constructor_SnapshotsRowsForOneAddressFamily()
    {
        List<RuleRowProjection> rows = [Row(FirewallAddressFamily.IPv4, 1)];

        RuleFamilyProjection projection = new(FirewallAddressFamily.IPv4, rows);
        rows.Add(Row(FirewallAddressFamily.IPv4, 2));

        Assert.HasCount(1, projection.Rows);
        Assert.AreEqual(FirewallAddressFamily.IPv4, projection.Rows[0].AddressFamily);
    }

    [TestMethod]
    public void Constructor_MixedAddressFamiliesAreRejected()
    {
        RuleRowProjection[] rows =
        [
            Row(FirewallAddressFamily.IPv4, 1),
            Row(FirewallAddressFamily.IPv6, 2),
        ];

        Assert.ThrowsExactly<ArgumentException>(() => new RuleFamilyProjection(FirewallAddressFamily.IPv4, rows));
    }

    private static RuleRowProjection Row(FirewallAddressFamily family, int occurrenceId) => new(
        new ListedFirewallRule(),
        family,
        occurrenceId,
        occurrenceId + 1,
        2,
        CanOrder: true,
        CanMutate: true,
        PositionChange: null);
}
