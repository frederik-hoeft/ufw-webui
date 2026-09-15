using Ufw.Client.RuleInsertion;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Tests.RuleInsertion;

[TestClass]
public sealed class RuleInsertionNavigationServiceTests
{
    private readonly RuleInsertionNavigationService _service = new();

    [TestMethod]
    public void BuildUri_UsesOccurrenceIdentityEvenWhenSemanticRuleIdsAreDuplicated()
    {
        ListedFirewallRule first = Rule("duplicate", FirewallAddressFamily.IPv4, 1);
        ListedFirewallRule second = Rule("duplicate", FirewallAddressFamily.IPv4, 2);
        RuleListResponse baseline = new(Active: true, [first, second], TestFirewallConfiguration.Enabled);

        string uri = _service.BuildUri(baseline, second, RuleInsertionPlacement.After);

        StringAssert.Contains(uri, $"baseline={Uri.EscapeDataString(FirewallRuleSnapshotFingerprint.Compute(baseline))}");
        StringAssert.Contains(uri, "anchor=1");
        StringAssert.Contains(uri, "placement=after");
    }

    [TestMethod]
    public void BuildUri_RejectsAnchorThatIsNotPartOfSnapshot()
    {
        ListedFirewallRule anchor = Rule("one", FirewallAddressFamily.IPv4, 1);
        RuleListResponse baseline = new(Active: true, [anchor], TestFirewallConfiguration.Enabled);
        ListedFirewallRule detachedCopy = Rule("one", FirewallAddressFamily.IPv4, 1);

        Assert.ThrowsExactly<InvalidOperationException>(() => _service.BuildUri(baseline, detachedCopy, RuleInsertionPlacement.Before));
    }

    [TestMethod]
    public void BuildUri_RejectsIpv6AnchorWhenCapabilityIsDisabled()
    {
        RuleListResponse baseline = new(Active: true, [Rule("v6", FirewallAddressFamily.IPv6, 1)], TestFirewallConfiguration.Disabled);

        Assert.ThrowsExactly<InvalidOperationException>(() => _service.BuildUri(baseline, baseline.Rules[0], RuleInsertionPlacement.Before));
    }

    [TestMethod]
    public void Resolve_RequiresExactFingerprintAndDerivesConcreteAnchorFamily()
    {
        RuleListResponse baseline = new(
            Active: true,
            [Rule("v4", FirewallAddressFamily.IPv4, 1), Rule("v6", FirewallAddressFamily.IPv6, 2)],
            TestFirewallConfiguration.Enabled);
        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);

        RuleInsertionNavigationResolution resolution = _service.Resolve(baseline, Query(fingerprint, "1", "before"));

        Assert.IsTrue(resolution.Succeeded);
        Assert.AreEqual(OrderedRuleInsertionContextError.None, resolution.Error);
        Assert.IsNotNull(resolution.Context);
        Assert.AreEqual(1, resolution.Context.AnchorOccurrenceId);
        Assert.AreEqual(RuleInsertionPlacement.Before, resolution.Context.Placement);
        Assert.AreEqual(FirewallAddressFamily.IPv6, resolution.Context.AddressFamily);
        Assert.AreSame(baseline.Rules[1], resolution.Context.Anchor);
    }

    [TestMethod]
    public void Resolve_RejectsIpv6AnchorWhenCapabilityIsDisabled()
    {
        RuleListResponse baseline = new(Active: true, [Rule("v6", FirewallAddressFamily.IPv6, 1)], TestFirewallConfiguration.Disabled);

        RuleInsertionNavigationResolution resolution = _service.Resolve(
            baseline,
            Query(FirewallRuleSnapshotFingerprint.Compute(baseline), "0", "before"));

        Assert.IsFalse(resolution.Succeeded);
        Assert.AreEqual(OrderedRuleInsertionContextError.CapabilityUnavailable, resolution.Error);
    }

    [TestMethod]
    public void Resolve_RejectsStaleSnapshotBeforeResolvingOccurrence()
    {
        RuleListResponse baseline = new(Active: true, [Rule("one", FirewallAddressFamily.IPv4, 1)], TestFirewallConfiguration.Enabled);
        RuleListResponse changed = new(
            Active: true,
            [Rule("one", FirewallAddressFamily.IPv4, 1), Rule("two", FirewallAddressFamily.IPv4, 2)],
            TestFirewallConfiguration.Enabled);

        RuleInsertionNavigationResolution resolution = _service.Resolve(
            changed,
            Query(FirewallRuleSnapshotFingerprint.Compute(baseline), "0", "before"));

        Assert.IsFalse(resolution.Succeeded);
        Assert.AreEqual(OrderedRuleInsertionContextError.StaleBaseline, resolution.Error);
    }

    [TestMethod]
    public void Resolve_RejectsOpaqueOrFamilyNeutralAnchor()
    {
        RuleListResponse opaque = new(
            Active: true,
            [new ListedFirewallRule { DisplayNumber = 1, Parsed = false, RawLine = "opaque" }],
            TestFirewallConfiguration.Enabled);
        RuleInsertionNavigationResolution opaqueResolution = _service.Resolve(
            opaque,
            Query(FirewallRuleSnapshotFingerprint.Compute(opaque), "0", "after"));
        Assert.AreEqual(OrderedRuleInsertionContextError.AnchorUnavailable, opaqueResolution.Error);

        RuleListResponse familyNeutral = new(Active: true, [Rule("any", FirewallAddressFamily.Any, 1)], TestFirewallConfiguration.Enabled);
        RuleInsertionNavigationResolution familyResolution = _service.Resolve(
            familyNeutral,
            Query(FirewallRuleSnapshotFingerprint.Compute(familyNeutral), "0", "after"));
        Assert.AreEqual(OrderedRuleInsertionContextError.AnchorUnavailable, familyResolution.Error);
    }

    [TestMethod]
    public void Resolve_RejectsIncompleteUnknownOrLegacyQuery()
    {
        RuleListResponse baseline = new(Active: true, [Rule("one", FirewallAddressFamily.IPv4, 1)], TestFirewallConfiguration.Enabled);
        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);

        Assert.AreEqual(OrderedRuleInsertionContextError.Incomplete, _service.Resolve(baseline, Query(fingerprint, null, "before")).Error);
        Assert.AreEqual(OrderedRuleInsertionContextError.InvalidPlacement, _service.Resolve(baseline, Query(fingerprint, "0", "sideways")).Error);
        Assert.AreEqual(OrderedRuleInsertionContextError.Incomplete, _service.Resolve(baseline, Query(fingerprint, "not-a-number", "before")).Error);
        Assert.AreEqual(
            OrderedRuleInsertionContextError.Incomplete,
            _service.Resolve(baseline, new RuleInsertionNavigationQuery(null, null, null, "legacy-id", null)).Error);
    }

    private static RuleInsertionNavigationQuery Query(string? fingerprint, string? occurrenceId, string? placement)
        => new(fingerprint, occurrenceId, placement, null, null);

    private static ListedFirewallRule Rule(string ruleId, FirewallAddressFamily family, int displayNumber) => new()
    {
        RuleId = ruleId,
        DisplayNumber = displayNumber,
        Parsed = true,
        RawLine = ruleId,
        Rule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = family,
            Direction = FirewallDirection.In,
        },
    };
}
