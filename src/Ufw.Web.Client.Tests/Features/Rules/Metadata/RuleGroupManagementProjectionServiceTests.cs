using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleGroupManagementProjectionServiceTests
{
    private readonly RuleGroupManagementProjectionService _projection = new(new RuleListProjectionService(new UfwRuleCommandRenderer()));

    [TestMethod]
    public void Create_WithoutRuleSnapshotPreservesMembersButMarksResolutionUnavailable()
    {
        RuleGroup group = new(Guid.CreateVersion7(), "operations", "managed", ["a", "b"]);

        RuleGroupManagementProjection result = _projection.Create([group], ruleSnapshot: null).Single();

        Assert.IsFalse(result.MemberResolutionAvailable);
        Assert.AreEqual(2, result.StoredMemberCount);
        Assert.AreEqual(0, result.LiveOccurrenceCount);
        Assert.AreEqual(0, result.StaleMembershipCount);
        CollectionAssert.AreEqual(new[] { "a", "b" }, result.Members.Select(static member => member.RuleId).ToArray());
    }

    [TestMethod]
    public void Create_ResolvesDuplicateOccurrencesAndSurfacesStaleSemanticMemberships()
    {
        RuleGroup group = new(Guid.CreateVersion7(), "operations", null, ["duplicate", "missing"]);
        ListedFirewallRule ipv4 = Rule("duplicate", FirewallAddressFamily.IPv4);
        ListedFirewallRule ipv6 = Rule("duplicate", FirewallAddressFamily.IPv6);
        RuleSnapshot snapshot = new(
            firewallActive: true,
            [ipv4, ipv6],
            new FirewallConfigurationSnapshot(true, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Deny));

        RuleGroupManagementProjection result = _projection.Create([group], snapshot).Single();

        Assert.IsTrue(result.MemberResolutionAvailable);
        Assert.AreEqual(2, result.StoredMemberCount);
        Assert.AreEqual(2, result.LiveOccurrenceCount);
        Assert.AreEqual(1, result.StaleMembershipCount);
        RuleGroupMemberProjection duplicate = result.Members.Single(static member => member.RuleId == "duplicate");
        Assert.HasCount(2, duplicate.Occurrences);
        Assert.IsFalse(duplicate.IsStale);
        Assert.IsTrue(result.Members.Single(static member => member.RuleId == "missing").IsStale);
    }

    [TestMethod]
    public void Create_ProvidesCanonicalCommandsAndFamilyLocalPositionsForResolvedOccurrences()
    {
        RuleGroup group = new(Guid.CreateVersion7(), "ssh", null, ["ssh-rule"]);
        ListedFirewallRule rule = new()
        {
            RuleId = "ssh-rule",
            Parsed = true,
            RawLine = "ssh-rule",
            Rule = new FirewallRuleSpecification
            {
                AddressFamily = FirewallAddressFamily.IPv4,
                Action = FirewallAction.Allow,
                Direction = FirewallDirection.In,
                DestinationPorts = "22",
                Protocol = FirewallProtocol.Tcp,
            },
        };
        RuleSnapshot snapshot = new(
            firewallActive: true,
            [rule],
            new FirewallConfigurationSnapshot(true, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Deny));

        RuleRowProjection occurrence = _projection.Create([group], snapshot).Single().Members.Single().Occurrences.Single();

        Assert.AreEqual(1, occurrence.FamilyPosition);
        Assert.AreEqual(FirewallAddressFamily.IPv4, occurrence.AddressFamily);
        Assert.AreEqual("allow in from 0.0.0.0/0 to 0.0.0.0/0 port 22 proto tcp", occurrence.CanonicalCommand);
    }

    private static ListedFirewallRule Rule(string ruleId, FirewallAddressFamily addressFamily) => new()
    {
        RuleId = ruleId,
        Parsed = true,
        RawLine = ruleId,
        Rule = new FirewallRuleSpecification
        {
            AddressFamily = addressFamily,
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
        },
    };
}
