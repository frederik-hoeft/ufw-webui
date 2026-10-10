using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Replacement;

namespace Ufw.Web.Client.Tests.Features.Rules.Replacement;

[TestClass]
public sealed class RuleReplacementNavigationServiceTests
{
    private readonly RuleReplacementNavigationService _service = new();

    [TestMethod]
    public void BuildUri_BindsFingerprintOccurrenceAndOriginalSemanticIdentity()
    {
        ListedFirewallRule first = Rule("first", FirewallAddressFamily.IPv4, 1);
        ListedFirewallRule target = Rule("target", FirewallAddressFamily.IPv6, 2);
        RuleListResponse baseline = new(Active: true, [first, target], TestFirewallConfiguration.Enabled);

        string uri = _service.BuildUri(baseline, 1);

        StringAssert.Contains(uri, $"baseline={Uri.EscapeDataString(FirewallRuleSnapshotFingerprint.Compute(baseline))}");
        StringAssert.Contains(uri, "target=1");
        StringAssert.Contains(uri, "ruleId=target");
    }

    [TestMethod]
    public void BuildUri_RejectsMissingOccurrenceOrDuplicateIdentity()
    {
        ListedFirewallRule target = Rule("duplicate", FirewallAddressFamily.IPv4, 1);
        ListedFirewallRule duplicate = Rule("duplicate", FirewallAddressFamily.IPv4, 2);
        RuleListResponse baseline = new(Active: true, [target, duplicate], TestFirewallConfiguration.Enabled);

        Assert.ThrowsExactly<InvalidOperationException>(() => _service.BuildUri(baseline, -1));
        Assert.ThrowsExactly<InvalidOperationException>(() => _service.BuildUri(baseline, 2));
        Assert.ThrowsExactly<InvalidOperationException>(() => _service.BuildUri(baseline, 0));
        Assert.ThrowsExactly<InvalidOperationException>(() => _service.BuildUri(baseline, 1));
    }

    [TestMethod]
    public void BuildUri_RejectsIpv6TargetWhenCapabilityIsDisabled()
    {
        RuleListResponse baseline = new(Active: true, [Rule("v6", FirewallAddressFamily.IPv6, 1)], TestFirewallConfiguration.Disabled);

        Assert.ThrowsExactly<InvalidOperationException>(() => _service.BuildUri(baseline, 0));
    }

    [TestMethod]
    public void Resolve_RequiresExactSnapshotAndDerivesTargetContext()
    {
        RuleListResponse baseline = new(
            Active: true,
            [
                Rule("v4-a", FirewallAddressFamily.IPv4, 1),
                Rule("v6-a", FirewallAddressFamily.IPv6, 2),
                Rule("v4-b", FirewallAddressFamily.IPv4, 3),
                Rule("v6-b", FirewallAddressFamily.IPv6, 4),
            ],
            TestFirewallConfiguration.Enabled);
        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);

        RuleReplacementNavigationResolution resolution = _service.Resolve(baseline, new RuleReplacementNavigationQuery(fingerprint, "3", "v6-b"));

        Assert.IsTrue(resolution.Succeeded);
        Assert.AreEqual(RuleReplacementContextError.None, resolution.Error);
        Assert.IsNotNull(resolution.Context);
        Assert.AreEqual(3, resolution.Context.TargetOccurrenceId);
        Assert.AreEqual(2, resolution.Context.TargetFamilyPosition);
        Assert.AreEqual("v6-b", resolution.Context.OriginalRuleId);
        Assert.AreEqual(FirewallAddressFamily.IPv6, resolution.Context.AddressFamily);
        Assert.AreSame(baseline.Rules[3], resolution.Context.Target);
    }

    [TestMethod]
    public void Resolve_RejectsStaleSnapshotBeforeResolvingTarget()
    {
        RuleListResponse baseline = new(Active: true, [Rule("one", FirewallAddressFamily.IPv4, 1)], TestFirewallConfiguration.Enabled);
        RuleListResponse changed = new(Active: true, [Rule("one", FirewallAddressFamily.IPv4, 1), Rule("two", FirewallAddressFamily.IPv4, 2)], TestFirewallConfiguration.Enabled);

        RuleReplacementNavigationResolution resolution = _service.Resolve(
            changed,
            new RuleReplacementNavigationQuery(FirewallRuleSnapshotFingerprint.Compute(baseline), "0", "one"));

        Assert.IsFalse(resolution.Succeeded);
        Assert.AreEqual(RuleReplacementContextError.StaleBaseline, resolution.Error);
    }

    [TestMethod]
    public void Resolve_RejectsMismatchedSemanticIdentity()
    {
        RuleListResponse baseline = new(Active: true, [Rule("one", FirewallAddressFamily.IPv4, 1)], TestFirewallConfiguration.Enabled);

        RuleReplacementNavigationResolution resolution = _service.Resolve(
            baseline,
            new RuleReplacementNavigationQuery(FirewallRuleSnapshotFingerprint.Compute(baseline), "0", "different"));

        Assert.IsFalse(resolution.Succeeded);
        Assert.AreEqual(RuleReplacementContextError.TargetMismatch, resolution.Error);
    }

    [TestMethod]
    public void Resolve_RejectsDuplicateSemanticIdentity()
    {
        RuleListResponse baseline = new(
            Active: true,
            [Rule("duplicate", FirewallAddressFamily.IPv4, 1), Rule("duplicate", FirewallAddressFamily.IPv4, 2)],
            TestFirewallConfiguration.Enabled);

        RuleReplacementNavigationResolution resolution = _service.Resolve(
            baseline,
            new RuleReplacementNavigationQuery(FirewallRuleSnapshotFingerprint.Compute(baseline), "1", "duplicate"));

        Assert.IsFalse(resolution.Succeeded);
        Assert.AreEqual(RuleReplacementContextError.DuplicateIdentity, resolution.Error);
    }

    [TestMethod]
    public void Resolve_RejectsOpaqueFamilyNeutralOrUnavailableIpv6Target()
    {
        RuleListResponse opaque = new(
            Active: true,
            [new ListedFirewallRule { RuleId = "opaque", DisplayNumber = 1, Parsed = false, RawLine = "opaque" }],
            TestFirewallConfiguration.Enabled);
        Assert.AreEqual(
            RuleReplacementContextError.TargetUnavailable,
            _service.Resolve(opaque, new RuleReplacementNavigationQuery(FirewallRuleSnapshotFingerprint.Compute(opaque), "0", "opaque")).Error);

        RuleListResponse familyNeutral = new(Active: true, [Rule("any", FirewallAddressFamily.Any, 1)], TestFirewallConfiguration.Enabled);
        Assert.AreEqual(
            RuleReplacementContextError.TargetUnavailable,
            _service.Resolve(familyNeutral, new RuleReplacementNavigationQuery(FirewallRuleSnapshotFingerprint.Compute(familyNeutral), "0", "any")).Error);

        RuleListResponse disabledIpv6 = new(Active: true, [Rule("v6", FirewallAddressFamily.IPv6, 1)], TestFirewallConfiguration.Disabled);
        Assert.AreEqual(
            RuleReplacementContextError.CapabilityUnavailable,
            _service.Resolve(disabledIpv6, new RuleReplacementNavigationQuery(FirewallRuleSnapshotFingerprint.Compute(disabledIpv6), "0", "v6")).Error);
    }

    [TestMethod]
    public void Resolve_RejectsIncompleteOrMalformedQuery()
    {
        RuleListResponse baseline = new(Active: true, [Rule("one", FirewallAddressFamily.IPv4, 1)], TestFirewallConfiguration.Enabled);
        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);

        Assert.AreEqual(RuleReplacementContextError.Incomplete, _service.Resolve(baseline, new RuleReplacementNavigationQuery(fingerprint, null, "one")).Error);
        Assert.AreEqual(RuleReplacementContextError.Incomplete, _service.Resolve(baseline, new RuleReplacementNavigationQuery(fingerprint, "x", "one")).Error);
        Assert.AreEqual(RuleReplacementContextError.InvalidFingerprint, _service.Resolve(baseline, new RuleReplacementNavigationQuery("invalid", "0", "one")).Error);
    }

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
