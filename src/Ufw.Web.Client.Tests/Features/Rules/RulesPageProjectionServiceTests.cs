using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Client.Features.Rules.Services;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RulesPageProjectionServiceTests
{
    private readonly RulesPageProjectionService _projection = new(new RuleListProjectionService(new UfwRuleCommandRenderer()), new RuleQueryService([new ActionRuleFilterEvaluator()]));

    [TestMethod]
    public void Create_NoSnapshotProducesEmptyUnavailableFamilies()
    {
        RulesPageProjection projection = _projection.Create(snapshot: null, orderingPreview: null, RuleQuery.Empty, []);

        Assert.IsEmpty(projection.IPv4Family.Rows);
        Assert.IsEmpty(projection.IPv6Family.Rows);
        Assert.IsEmpty(projection.IPv4Query.Rows);
        Assert.IsEmpty(projection.IPv6Query.Rows);
        Assert.IsFalse(projection.IPv6Available);
    }

    [TestMethod]
    public void Create_DerivesFamilyAvailabilityAndMetadataFromOneSnapshot()
    {
        Guid metadataId = Guid.CreateVersion7();
        ListedFirewallRule ipv4 = Rule("v4", FirewallAddressFamily.IPv4);
        ListedFirewallRule ipv6 = Rule("v6", FirewallAddressFamily.IPv6);
        RuleMetadata metadata = new(metadataId, "managed", []);
        RuleSnapshot snapshot = new(true, [ipv4, ipv6], TestFirewallConfiguration.Disabled, new Dictionary<string, RuleMetadata>(StringComparer.Ordinal) { ["v4"] = metadata });

        RulesPageProjection projection = _projection.Create(snapshot, orderingPreview: null, RuleQuery.Empty, []);

        Assert.HasCount(1, projection.IPv4Family.Rows);
        Assert.HasCount(1, projection.IPv6Family.Rows);
        Assert.AreSame(metadata, projection.IPv4Family.Rows.Single().Metadata);
        Assert.IsTrue(projection.IPv6Available);
        Assert.AreEqual(1, projection.IPv4Query.TotalCount);
        Assert.AreEqual(1, projection.IPv6Query.TotalCount);
    }

    [TestMethod]
    public void Create_EnabledIpv6CapabilityKeepsEmptyIpv6FamilyAvailable()
    {
        RuleSnapshot snapshot = new(true, [Rule("v4", FirewallAddressFamily.IPv4)], TestFirewallConfiguration.Enabled);

        RulesPageProjection projection = _projection.Create(snapshot, orderingPreview: null, RuleQuery.Empty, []);

        Assert.IsTrue(projection.IPv6Available);
        Assert.IsEmpty(projection.IPv6Family.Rows);
    }

    [TestMethod]
    public void Create_AppliesOrderingPreviewBeforeQueryProjection()
    {
        ListedFirewallRule denied = Rule("deny", FirewallAddressFamily.IPv4, FirewallAction.Deny);
        ListedFirewallRule allowed = Rule("allow", FirewallAddressFamily.IPv4, FirewallAction.Allow);
        RuleSnapshot snapshot = new(true, [denied, allowed], TestFirewallConfiguration.Enabled);
        RuleOrderingPreview preview = new([1, 0], new HashSet<int> { 1 });
        RuleQuery query = new([new ActionRuleFilter(FirewallAction.Allow)]);

        RulesPageProjection projection = _projection.Create(snapshot, preview, query, []);

        Assert.AreSame(allowed, projection.IPv4Family.Rows[0].Rule);
        Assert.HasCount(1, projection.IPv4Query.Rows);
        Assert.AreSame(allowed, projection.IPv4Query.Rows[0].Row.Rule);
        Assert.AreEqual(2, projection.IPv4Query.TotalCount);
        Assert.AreEqual(new RulePositionChange(2, 1, DirectlyMoved: true), projection.IPv4Query.Rows[0].Row.PositionChange);
    }

    private static ListedFirewallRule Rule(string id, FirewallAddressFamily family, FirewallAction action = FirewallAction.Allow) => new()
    {
        RuleId = id,
        DisplayNumber = 1,
        Parsed = true,
        RawLine = id,
        Rule = new FirewallRuleSpecification { AddressFamily = family, Action = action },
    };
}
