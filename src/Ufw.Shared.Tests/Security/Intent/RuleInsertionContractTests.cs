using Ufw.Shared.Firewall;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Tests.Security.Intent;

[TestClass]
public sealed class RuleInsertionContractTests
{
    private const string VALID_FINGERPRINT = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [TestMethod]
    public void TryResolveAnchor_ValidConcreteSameFamily_ReturnsExactOccurrence()
    {
        ListedFirewallRule duplicate = Rule(FirewallAddressFamily.IPv4, "same");
        ListedFirewallRule expected = Rule(FirewallAddressFamily.IPv4, "same");
        ListedFirewallRule[] baseline = [duplicate, expected];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4, anchorOccurrenceId: 1);

        bool resolved = RuleInsertionContract.TryResolveAnchor(baseline, payload, out ListedFirewallRule? actual, out string? diagnostic);

        Assert.IsTrue(resolved);
        Assert.AreSame(expected, actual);
        Assert.IsNull(diagnostic);
    }

    [TestMethod]
    public void ValidatePayload_InvalidFingerprint_Rejects()
    {
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4);
        payload.BaselineFingerprint = "not-a-fingerprint";

        Assert.Throws<ArgumentException>(() => RuleInsertionContract.ValidatePayload(payload));
    }

    [TestMethod]
    public void ValidatePayload_NegativeOccurrence_Rejects()
    {
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4, anchorOccurrenceId: -1);

        Assert.Throws<ArgumentOutOfRangeException>(() => RuleInsertionContract.ValidatePayload(payload));
    }

    [TestMethod]
    public void ValidatePayload_UnknownPlacement_Rejects()
    {
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4);
        payload.Placement = (RuleInsertionPlacement)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => RuleInsertionContract.ValidatePayload(payload));
    }

    [TestMethod]
    public void ValidatePayload_FamilyNeutralRule_Rejects()
    {
        InsertRulePayload payload = Payload(FirewallAddressFamily.Any);

        Assert.Throws<ArgumentException>(() => RuleInsertionContract.ValidatePayload(payload));
    }

    [TestMethod]
    public void TryResolveAnchor_OutOfRangeOccurrence_ReturnsFailure()
    {
        ListedFirewallRule[] baseline = [Rule(FirewallAddressFamily.IPv4, "one")];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4, anchorOccurrenceId: 1);

        bool resolved = RuleInsertionContract.TryResolveAnchor(baseline, payload, out ListedFirewallRule? anchor, out string? diagnostic);

        Assert.IsFalse(resolved);
        Assert.IsNull(anchor);
        Assert.AreEqual("Anchor occurrence is outside the signed baseline.", diagnostic);
    }

    [TestMethod]
    public void TryResolveAnchor_UnparsedOccurrence_ReturnsFailure()
    {
        ListedFirewallRule[] baseline = [new() { DisplayNumber = 1, Parsed = false, RawLine = "opaque" }];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4);

        bool resolved = RuleInsertionContract.TryResolveAnchor(baseline, payload, out ListedFirewallRule? anchor, out string? diagnostic);

        Assert.IsFalse(resolved);
        Assert.IsNull(anchor);
        Assert.AreEqual("Ordered insertion requires a parsed anchor rule with a concrete address family.", diagnostic);
    }

    [TestMethod]
    public void TryResolveAnchor_FamilyMismatch_ReturnsFailure()
    {
        ListedFirewallRule[] baseline = [Rule(FirewallAddressFamily.IPv6, "v6")];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4);

        bool resolved = RuleInsertionContract.TryResolveAnchor(baseline, payload, out ListedFirewallRule? anchor, out string? diagnostic);

        Assert.IsFalse(resolved);
        Assert.IsNull(anchor);
        Assert.AreEqual("Ordered insertion rule address family must match the anchor address family.", diagnostic);
    }

    [TestMethod]
    public void TryResolveAnchor_FamilyNeutralAnchor_ReturnsFailure()
    {
        ListedFirewallRule[] baseline = [Rule(FirewallAddressFamily.Any, "any")];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4);

        bool resolved = RuleInsertionContract.TryResolveAnchor(baseline, payload, out ListedFirewallRule? anchor, out string? diagnostic);

        Assert.IsFalse(resolved);
        Assert.IsNull(anchor);
        Assert.AreEqual("Ordered insertion requires an anchor with a concrete address family.", diagnostic);
    }

    private static InsertRulePayload Payload(FirewallAddressFamily family, int anchorOccurrenceId = 0) => new()
    {
        BaselineFingerprint = VALID_FINGERPRINT,
        AnchorOccurrenceId = anchorOccurrenceId,
        Placement = RuleInsertionPlacement.Before,
        Rule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = family,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "443",
        },
    };

    private static ListedFirewallRule Rule(FirewallAddressFamily family, string rawLine) => new()
    {
        DisplayNumber = 1,
        Parsed = true,
        RawLine = rawLine,
        RuleId = "duplicate",
        Rule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = family,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "22",
        },
    };
}
