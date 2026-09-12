using Ufw.Client.RuleInsertion;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Tests.RuleInsertion;

[TestClass]
public sealed class OrderedRuleInsertionNavigationTests
{
    [TestMethod]
    public void BuildUri_UsesOccurrenceIdentityEvenWhenSemanticRuleIdsAreDuplicated()
    {
        ListedFirewallRule first = Rule("duplicate", FirewallAddressFamily.IPv4, 1);
        ListedFirewallRule second = Rule("duplicate", FirewallAddressFamily.IPv4, 2);
        RuleListResponse baseline = new(Active: true, [first, second]);

        string uri = OrderedRuleInsertionNavigation.BuildUri(baseline, 1, RuleInsertionPlacement.After);

        StringAssert.Contains(uri, $"baseline={Uri.EscapeDataString(FirewallRuleSnapshotFingerprint.Compute(baseline))}");
        StringAssert.Contains(uri, "anchor=1");
        StringAssert.Contains(uri, "placement=after");
    }

    [TestMethod]
    public void TryResolve_RequiresExactFingerprintAndDerivesConcreteAnchorFamily()
    {
        RuleListResponse baseline = new(
            Active: true,
            [
                Rule("v4", FirewallAddressFamily.IPv4, 1),
                Rule("v6", FirewallAddressFamily.IPv6, 2),
            ]);
        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);

        bool success = OrderedRuleInsertionNavigation.TryResolve(
            baseline,
            fingerprint,
            anchorOccurrenceId: 1,
            "before",
            out OrderedRuleInsertionNavigationContext? context,
            out OrderedRuleInsertionContextError error);

        Assert.IsTrue(success);
        Assert.AreEqual(OrderedRuleInsertionContextError.None, error);
        Assert.IsNotNull(context);
        Assert.AreEqual(1, context.AnchorOccurrenceId);
        Assert.AreEqual(RuleInsertionPlacement.Before, context.Placement);
        Assert.AreEqual(FirewallAddressFamily.IPv6, context.AddressFamily);
        Assert.AreSame(baseline.Rules[1], context.Anchor);
    }

    [TestMethod]
    public void TryResolve_RejectsStaleSnapshotBeforeResolvingOccurrence()
    {
        RuleListResponse baseline = new(Active: true, [Rule("one", FirewallAddressFamily.IPv4, 1)]);
        RuleListResponse changed = new(
            Active: true,
            [
                Rule("one", FirewallAddressFamily.IPv4, 1),
                Rule("two", FirewallAddressFamily.IPv4, 2),
            ]);

        bool success = OrderedRuleInsertionNavigation.TryResolve(
            changed,
            FirewallRuleSnapshotFingerprint.Compute(baseline),
            anchorOccurrenceId: 0,
            "before",
            out OrderedRuleInsertionNavigationContext? context,
            out OrderedRuleInsertionContextError error);

        Assert.IsFalse(success);
        Assert.IsNull(context);
        Assert.AreEqual(OrderedRuleInsertionContextError.StaleBaseline, error);
    }

    [TestMethod]
    public void TryResolve_RejectsOpaqueOrFamilyNeutralAnchor()
    {
        RuleListResponse opaque = new(
            Active: true,
            [new ListedFirewallRule { DisplayNumber = 1, Parsed = false, RawLine = "opaque" }]);
        Assert.IsFalse(OrderedRuleInsertionNavigation.TryResolve(
            opaque,
            FirewallRuleSnapshotFingerprint.Compute(opaque),
            anchorOccurrenceId: 0,
            "after",
            out _,
            out OrderedRuleInsertionContextError opaqueError));
        Assert.AreEqual(OrderedRuleInsertionContextError.AnchorUnavailable, opaqueError);

        RuleListResponse familyNeutral = new(
            Active: true,
            [Rule("any", FirewallAddressFamily.Any, 1)]);
        Assert.IsFalse(OrderedRuleInsertionNavigation.TryResolve(
            familyNeutral,
            FirewallRuleSnapshotFingerprint.Compute(familyNeutral),
            anchorOccurrenceId: 0,
            "after",
            out _,
            out OrderedRuleInsertionContextError familyError));
        Assert.AreEqual(OrderedRuleInsertionContextError.AnchorUnavailable, familyError);
    }

    [TestMethod]
    public void TryResolve_RejectsIncompleteOrUnknownPlacement()
    {
        RuleListResponse baseline = new(Active: true, [Rule("one", FirewallAddressFamily.IPv4, 1)]);
        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);

        Assert.IsFalse(OrderedRuleInsertionNavigation.TryResolve(
            baseline,
            fingerprint,
            anchorOccurrenceId: null,
            "before",
            out _,
            out OrderedRuleInsertionContextError incomplete));
        Assert.AreEqual(OrderedRuleInsertionContextError.Incomplete, incomplete);

        Assert.IsFalse(OrderedRuleInsertionNavigation.TryResolve(
            baseline,
            fingerprint,
            anchorOccurrenceId: 0,
            "sideways",
            out _,
            out OrderedRuleInsertionContextError invalidPlacement));
        Assert.AreEqual(OrderedRuleInsertionContextError.InvalidPlacement, invalidPlacement);
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
