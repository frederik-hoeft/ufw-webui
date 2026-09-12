using Ufw.Shared.Firewall;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Tests.Security.Intent;

[TestClass]
public sealed class RuleInsertionContractTests
{
    private const string VALID_FINGERPRINT = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [TestMethod]
    public void ResolveAnchor_ValidConcreteSameFamily_ReturnsExactOccurrence()
    {
        ListedFirewallRule duplicate = Rule(FirewallAddressFamily.IPv4, "same");
        ListedFirewallRule expected = Rule(FirewallAddressFamily.IPv4, "same");
        ListedFirewallRule[] baseline = [duplicate, expected];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4, anchorOccurrenceId: 1);

        ListedFirewallRule actual = RuleInsertionContract.ResolveAnchor(baseline, payload);

        Assert.AreSame(expected, actual);
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
    public void ResolveAnchor_OutOfRangeOccurrence_Rejects()
    {
        ListedFirewallRule[] baseline = [Rule(FirewallAddressFamily.IPv4, "one")];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4, anchorOccurrenceId: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => RuleInsertionContract.ResolveAnchor(baseline, payload));
    }

    [TestMethod]
    public void ResolveAnchor_UnparsedOccurrence_Rejects()
    {
        ListedFirewallRule[] baseline = [new() { DisplayNumber = 1, Parsed = false, RawLine = "opaque" }];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4);

        Assert.Throws<InvalidOperationException>(() => RuleInsertionContract.ResolveAnchor(baseline, payload));
    }

    [TestMethod]
    public void ResolveAnchor_FamilyMismatch_Rejects()
    {
        ListedFirewallRule[] baseline = [Rule(FirewallAddressFamily.IPv6, "v6")];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4);

        Assert.Throws<InvalidOperationException>(() => RuleInsertionContract.ResolveAnchor(baseline, payload));
    }

    [TestMethod]
    public void ResolveAnchor_FamilyNeutralAnchor_Rejects()
    {
        ListedFirewallRule[] baseline = [Rule(FirewallAddressFamily.Any, "any")];
        InsertRulePayload payload = Payload(FirewallAddressFamily.IPv4);

        Assert.Throws<InvalidOperationException>(() => RuleInsertionContract.ResolveAnchor(baseline, payload));
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
