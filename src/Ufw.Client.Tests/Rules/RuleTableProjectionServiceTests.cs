using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Tests.Rules;

[TestClass]
public sealed class RuleTableProjectionServiceTests
{
    private readonly RuleTableProjectionService _projection = new();

    [TestMethod]
    public void Create_DuplicateSemanticIdsRemainOrderableButAreNotMutable()
    {
        ListedFirewallRule first = Rule("duplicate", FirewallAddressFamily.IPv4, displayNumber: 1);
        ListedFirewallRule second = Rule("duplicate", FirewallAddressFamily.IPv4, displayNumber: 2);

        RuleTableProjection projection = _projection.Create([first, second], orderingPreview: null);

        Assert.HasCount(1, projection.Families);
        Assert.IsTrue(projection.Families[0].Rows.All(static row => row.CanOrder));
        Assert.IsTrue(projection.Families[0].Rows.All(static row => !row.CanMutate));
    }

    [TestMethod]
    public void Create_OpaqueRowsRemainVisibleButCannotBeReorderedOrMutated()
    {
        ListedFirewallRule opaque = new()
        {
            DisplayNumber = 1,
            Parsed = false,
            RawLine = "opaque",
        };

        RuleRowProjection row = _projection.Create([opaque], orderingPreview: null).Families[0].Rows[0];

        Assert.IsFalse(row.CanOrder);
        Assert.IsFalse(row.CanMutate);
        Assert.AreEqual(0, row.OccurrenceId);
        Assert.AreEqual(1, row.FamilyPosition);
    }

    [TestMethod]
    public void Create_GroupsInterleavedRowsByObservedFamilyAndComputesIndependentFamilyPositions()
    {
        ListedFirewallRule ipv4First = Rule("v4-a", FirewallAddressFamily.IPv4, displayNumber: 1);
        ListedFirewallRule ipv6First = Rule("v6-a", FirewallAddressFamily.IPv6, displayNumber: 2);
        ListedFirewallRule ipv4Second = Rule("v4-b", FirewallAddressFamily.IPv4, displayNumber: 3);
        ListedFirewallRule ipv6Second = Rule("v6-b", FirewallAddressFamily.IPv6, displayNumber: 4);

        RuleTableProjection projection = _projection.Create([ipv4First, ipv6First, ipv4Second, ipv6Second], orderingPreview: null);

        Assert.HasCount(2, projection.Families);
        Assert.AreEqual(FirewallAddressFamily.IPv4, projection.Families[0].AddressFamily);
        CollectionAssert.AreEqual(new[] { 1, 2 }, projection.Families[0].Rows.Select(static row => row.FamilyPosition).ToArray());
        Assert.IsTrue(projection.Families[0].Rows.All(static row => row.FamilyCount == 2));
        Assert.AreEqual(FirewallAddressFamily.IPv6, projection.Families[1].AddressFamily);
        CollectionAssert.AreEqual(new[] { 1, 2 }, projection.Families[1].Rows.Select(static row => row.FamilyPosition).ToArray());
        Assert.IsTrue(projection.Families[1].Rows.All(static row => row.FamilyCount == 2));
    }

    [TestMethod]
    public void Create_OrderingPreviewReportsOnlyFamilyLocalPositionChanges()
    {
        ListedFirewallRule ipv4First = Rule("v4-a", FirewallAddressFamily.IPv4, displayNumber: 1);
        ListedFirewallRule ipv6First = Rule("v6-a", FirewallAddressFamily.IPv6, displayNumber: 2);
        ListedFirewallRule ipv4Second = Rule("v4-b", FirewallAddressFamily.IPv4, displayNumber: 3);
        ListedFirewallRule ipv6Second = Rule("v6-b", FirewallAddressFamily.IPv6, displayNumber: 4);
        ListedFirewallRule[] authoritative = [ipv4First, ipv6First, ipv4Second, ipv6Second];
        RuleOrderingPreview preview = new([2, 1, 0, 3], new HashSet<int> { 2 });

        RuleTableProjection projection = _projection.Create(authoritative, preview);
        RuleFamilyProjection ipv4 = projection.Families.Single(static family => family.AddressFamily == FirewallAddressFamily.IPv4);
        RuleFamilyProjection ipv6 = projection.Families.Single(static family => family.AddressFamily == FirewallAddressFamily.IPv6);

        CollectionAssert.AreEqual(new[] { "v4-b", "v4-a" }, ipv4.Rows.Select(static row => row.Rule.RuleId).ToArray());
        Assert.AreEqual(new RulePositionChange(2, 1, DirectlyMoved: true), ipv4.Rows[0].PositionChange);
        Assert.AreEqual(new RulePositionChange(1, 2, DirectlyMoved: false), ipv4.Rows[1].PositionChange);
        Assert.IsTrue(ipv6.Rows.All(static row => row.PositionChange is null));
        CollectionAssert.AreEqual(new int?[] { 1, 2, 3, 4 }, authoritative.Select(static rule => rule.DisplayNumber).ToArray());
    }

    [TestMethod]
    public void Create_OrderingPreviewReportsIndirectFamilyShiftForOpaqueRow()
    {
        ListedFirewallRule first = Rule("first", FirewallAddressFamily.IPv4, displayNumber: 1);
        ListedFirewallRule opaque = new()
        {
            DisplayNumber = 2,
            Parsed = false,
            RawLine = "opaque",
        };
        ListedFirewallRule third = Rule("third", FirewallAddressFamily.IPv4, displayNumber: 3);
        RuleOrderingPreview preview = new([2, 0, 1], new HashSet<int> { 2 });

        RuleTableProjection projection = _projection.Create([first, opaque, third], preview);
        RuleRowProjection projectedOpaque = projection.Families[0].Rows.Single(static row => !row.Rule.Parsed);

        Assert.AreEqual(3, projectedOpaque.FamilyPosition);
        Assert.AreEqual(new RulePositionChange(2, 3, DirectlyMoved: false), projectedOpaque.PositionChange);
        Assert.AreEqual(2, projectedOpaque.Rule.DisplayNumber);
    }

    [TestMethod]
    public void Create_UsesOrderingPreviewForOriginalOccurrenceAndMovePresentation()
    {
        ListedFirewallRule first = Rule("first", FirewallAddressFamily.IPv4, displayNumber: 1);
        ListedFirewallRule second = Rule("second", FirewallAddressFamily.IPv4, displayNumber: 2);
        ListedFirewallRule[] authoritative = [first, second];
        RuleOrderingPreview preview = new([1, 0], new HashSet<int> { 1 });

        RuleTableProjection projection = _projection.Create(authoritative, preview);
        RuleRowProjection moved = projection.Families[0].Rows[0];

        Assert.AreSame(second, moved.Rule);
        Assert.AreEqual(2, moved.Rule.DisplayNumber);
        Assert.AreEqual(1, first.DisplayNumber);
        Assert.AreEqual(1, moved.OccurrenceId);
        Assert.AreEqual(1, moved.FamilyPosition);
        Assert.IsNotNull(moved.PositionChange);
        Assert.AreEqual(2, moved.PositionChange.OriginalPosition);
        Assert.AreEqual(1, moved.PositionChange.CurrentPosition);
        Assert.IsTrue(moved.PositionChange.DirectlyMoved);
    }

    private static ListedFirewallRule Rule(string ruleId, FirewallAddressFamily family, int displayNumber) => new()
    {
        RuleId = ruleId,
        DisplayNumber = displayNumber,
        Parsed = true,
        RawLine = ruleId,
        Rule = new FirewallRuleSpecification { AddressFamily = family },
    };
}
