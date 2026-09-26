using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RuleListProjectionServiceTests
{
    private readonly RuleListProjectionService _projection = new(new UfwRuleCommandRenderer());

    [TestMethod]
    public void Create_AlwaysExposesBothAddressFamilyPartitions()
    {
        RuleListProjection projection = _projection.Create([], orderingPreview: null);

        Assert.HasCount(2, projection.Families);
        Assert.IsEmpty(projection.GetFamily(FirewallAddressFamily.IPv4).Rows);
        Assert.IsEmpty(projection.GetFamily(FirewallAddressFamily.IPv6).Rows);
    }

    [TestMethod]
    public void Create_DuplicateSemanticIdsRemainOrderableButAreNotMutable()
    {
        ListedFirewallRule first = Rule("duplicate", FirewallAddressFamily.IPv4, displayNumber: 1);
        ListedFirewallRule second = Rule("duplicate", FirewallAddressFamily.IPv4, displayNumber: 2);

        RuleListProjection projection = _projection.Create([first, second], orderingPreview: null);

        Assert.HasCount(2, projection.Families);
        RuleFamilyProjection ipv4 = projection.GetFamily(FirewallAddressFamily.IPv4);
        Assert.IsTrue(ipv4.Rows.All(static row => row.CanOrder));
        Assert.IsTrue(ipv4.Rows.All(static row => !row.CanMutate));
        Assert.IsEmpty(projection.GetFamily(FirewallAddressFamily.IPv6).Rows);
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

        RuleListProjection projection = _projection.Create([ipv4First, ipv6First, ipv4Second, ipv6Second], orderingPreview: null);

        Assert.HasCount(2, projection.Families);
        Assert.AreEqual(FirewallAddressFamily.IPv4, projection.Families[0].AddressFamily);
        CollectionAssert.AreEqual(new[] { 1, 2 }, projection.Families[0].Rows.Select(static row => row.FamilyPosition).ToArray());
        Assert.IsTrue(projection.Families[0].Rows.All(static row => row.FamilyCount == 2));
        Assert.AreEqual(FirewallAddressFamily.IPv6, projection.Families[1].AddressFamily);
        CollectionAssert.AreEqual(new[] { 1, 2 }, projection.Families[1].Rows.Select(static row => row.FamilyPosition).ToArray());
        Assert.IsTrue(projection.Families[1].Rows.All(static row => row.FamilyCount == 2));
    }

    [TestMethod]
    public void Create_AttachesMetadataBySemanticIdentityToEveryDuplicateOccurrence()
    {
        ListedFirewallRule first = Rule("shared", FirewallAddressFamily.IPv4, displayNumber: 1);
        ListedFirewallRule second = Rule("shared", FirewallAddressFamily.IPv4, displayNumber: 2);
        RuleMetadata metadata = new(
            Guid.CreateVersion7(),
            "managed rule",
            [new RuleTag(Guid.CreateVersion7(), "prod", "#336699"), new RuleTag(Guid.CreateVersion7(), "ssh", "#663399")],
            new RuleGroupMembership(Guid.CreateVersion7(), "operations", null));
        Dictionary<string, RuleMetadata> metadataByRuleId = new(StringComparer.Ordinal)
        {
            ["shared"] = metadata,
        };

        RuleListProjection projection = _projection.Create([first, second], orderingPreview: null, metadataByRuleId);

        RuleFamilyProjection ipv4 = projection.GetFamily(FirewallAddressFamily.IPv4);
        Assert.IsTrue(ipv4.Rows.All(row => ReferenceEquals(metadata, row.Metadata)));
        Assert.IsTrue(ipv4.Rows.All(static row => row.Metadata?.Group?.Name == "operations"));
    }

    [TestMethod]
    public void Create_ComputesCanonicalCommandOnceForParsedRules()
    {
        ListedFirewallRule rule = new()
        {
            RuleId = "canonical",
            DisplayNumber = 1,
            Parsed = true,
            RawLine = "canonical",
            Rule = new FirewallRuleSpecification
            {
                AddressFamily = FirewallAddressFamily.IPv4,
                Action = FirewallAction.Allow,
                Direction = FirewallDirection.In,
                Source = "10.0.0.0/8",
                Destination = "192.0.2.10",
                DestinationPorts = "22",
                Protocol = FirewallProtocol.Tcp,
            },
        };

        RuleRowProjection row = _projection.Create([rule], orderingPreview: null).GetFamily(FirewallAddressFamily.IPv4).Rows.Single();

        Assert.AreEqual("allow in from 10.0.0.0/8 to 192.0.2.10 port 22 proto tcp", row.CanonicalCommand);
    }

    [TestMethod]
    public void Create_OpaqueRuleHasNoCanonicalCommand()
    {
        ListedFirewallRule opaque = new()
        {
            DisplayNumber = 1,
            Parsed = false,
            RawLine = "opaque",
        };

        RuleRowProjection row = _projection.Create([opaque], orderingPreview: null).Families[0].Rows.Single();

        Assert.IsNull(row.CanonicalCommand);
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

        RuleListProjection projection = _projection.Create(authoritative, preview);
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

        RuleListProjection projection = _projection.Create([first, opaque, third], preview);
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

        RuleListProjection projection = _projection.Create(authoritative, preview);
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
