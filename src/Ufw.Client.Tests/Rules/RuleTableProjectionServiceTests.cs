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
    public void Create_GroupsRowsByObservedFamilyAndComputesFamilyLocalPositions()
    {
        ListedFirewallRule ipv4First = Rule("v4-a", FirewallAddressFamily.IPv4, displayNumber: 1);
        ListedFirewallRule ipv4Second = Rule("v4-b", FirewallAddressFamily.IPv4, displayNumber: 2);
        ListedFirewallRule ipv6 = Rule("v6", FirewallAddressFamily.IPv6, displayNumber: 3);

        RuleTableProjection projection = _projection.Create([ipv4First, ipv4Second, ipv6], orderingPreview: null);

        Assert.HasCount(2, projection.Families);
        Assert.AreEqual(FirewallAddressFamily.IPv4, projection.Families[0].AddressFamily);
        CollectionAssert.AreEqual(new[] { 1, 2 }, projection.Families[0].Rows.Select(static row => row.FamilyPosition).ToArray());
        Assert.AreEqual(FirewallAddressFamily.IPv6, projection.Families[1].AddressFamily);
        Assert.AreEqual(1, projection.Families[1].Rows[0].FamilyPosition);
        Assert.AreEqual(1, projection.Families[1].Rows[0].FamilyCount);
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
