using Ufw.Shared.Firewall;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Tests.Security.Intent;

[TestClass]
public sealed class RuleReplacementContractTests
{
    private const string VALID_FINGERPRINT = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [TestMethod]
    public void TryResolveTarget_ValidExactOccurrence_ReturnsTarget()
    {
        ListedFirewallRule first = ListedRule(FirewallAddressFamily.IPv4, "22");
        ListedFirewallRule target = ListedRule(FirewallAddressFamily.IPv4, "443");
        ListedFirewallRule[] baseline = [first, target];
        ReplaceRulePayload payload = Payload(target.RuleId!, FirewallAddressFamily.IPv4, targetOccurrenceId: 1);

        bool resolved = RuleReplacementContract.TryResolveTarget(baseline, payload, out ListedFirewallRule? actual, out string? diagnostic);

        Assert.IsTrue(resolved);
        Assert.AreSame(target, actual);
        Assert.IsNull(diagnostic);
    }

    [TestMethod]
    public void ValidatePayload_InvalidFingerprint_Rejects()
    {
        ReplaceRulePayload payload = Payload(ValidRuleId(), FirewallAddressFamily.IPv4);
        payload.BaselineFingerprint = "not-a-fingerprint";

        Assert.Throws<ArgumentException>(() => RuleReplacementContract.ValidatePayload(payload));
    }

    [TestMethod]
    public void ValidatePayload_NegativeOccurrence_Rejects()
    {
        ReplaceRulePayload payload = Payload(ValidRuleId(), FirewallAddressFamily.IPv4, targetOccurrenceId: -1);

        Assert.Throws<ArgumentOutOfRangeException>(() => RuleReplacementContract.ValidatePayload(payload));
    }

    [TestMethod]
    public void ValidatePayload_InvalidOriginalRuleId_Rejects()
    {
        ReplaceRulePayload payload = Payload("sha256:not-a-digest", FirewallAddressFamily.IPv4);

        Assert.Throws<ArgumentException>(() => RuleReplacementContract.ValidatePayload(payload));
    }

    [TestMethod]
    public void ValidatePayload_FamilyNeutralReplacement_Rejects()
    {
        ReplaceRulePayload payload = Payload(ValidRuleId(), FirewallAddressFamily.Any);

        Assert.Throws<ArgumentException>(() => RuleReplacementContract.ValidatePayload(payload));
    }

    [TestMethod]
    public void TryResolveTarget_OutOfRangeOccurrence_Rejects()
    {
        ReplaceRulePayload payload = Payload(ValidRuleId(), FirewallAddressFamily.IPv4, targetOccurrenceId: 1);

        bool resolved = RuleReplacementContract.TryResolveTarget([], payload, out ListedFirewallRule? target, out string? diagnostic);

        Assert.IsFalse(resolved);
        Assert.IsNull(target);
        StringAssert.Contains(diagnostic, "outside the signed baseline");
    }

    [TestMethod]
    public void TryResolveTarget_UnparsedTarget_Rejects()
    {
        ReplaceRulePayload payload = Payload(ValidRuleId(), FirewallAddressFamily.IPv4);
        ListedFirewallRule target = new() { Parsed = false, RawLine = "opaque" };

        bool resolved = RuleReplacementContract.TryResolveTarget([target], payload, out ListedFirewallRule? actual, out string? diagnostic);

        Assert.IsFalse(resolved);
        Assert.IsNull(actual);
        StringAssert.Contains(diagnostic, "parsed target rule");
    }

    [TestMethod]
    public void TryResolveTarget_OriginalIdentityMismatch_Rejects()
    {
        ListedFirewallRule target = ListedRule(FirewallAddressFamily.IPv4, "22");
        ReplaceRulePayload payload = Payload(RuleIdentity.Compute(Rule(FirewallAddressFamily.IPv4, "443")), FirewallAddressFamily.IPv4);

        bool resolved = RuleReplacementContract.TryResolveTarget([target], payload, out ListedFirewallRule? actual, out string? diagnostic);

        Assert.IsFalse(resolved);
        Assert.IsNull(actual);
        StringAssert.Contains(diagnostic, "identity does not match");
    }

    [TestMethod]
    public void TryResolveTarget_AddressFamilyChange_Rejects()
    {
        ListedFirewallRule target = ListedRule(FirewallAddressFamily.IPv4, "22");
        ReplaceRulePayload payload = Payload(target.RuleId!, FirewallAddressFamily.IPv6);

        bool resolved = RuleReplacementContract.TryResolveTarget([target], payload, out ListedFirewallRule? actual, out string? diagnostic);

        Assert.IsFalse(resolved);
        Assert.IsNull(actual);
        StringAssert.Contains(diagnostic, "address family must match");
    }

    private static ReplaceRulePayload Payload(string originalRuleId, FirewallAddressFamily family, int targetOccurrenceId = 0) => new()
    {
        BaselineFingerprint = VALID_FINGERPRINT,
        TargetOccurrenceId = targetOccurrenceId,
        OriginalRuleId = originalRuleId,
        ReplacementRule = Rule(family, "8080"),
    };

    private static ListedFirewallRule ListedRule(FirewallAddressFamily family, string port)
    {
        FirewallRuleSpecification rule = Rule(family, port);
        return new ListedFirewallRule { Parsed = true, RuleId = RuleIdentity.Compute(rule), Rule = rule };
    }

    private static FirewallRuleSpecification Rule(FirewallAddressFamily family, string port) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = family,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = port,
    };

    private static string ValidRuleId() => RuleIdentity.Compute(Rule(FirewallAddressFamily.IPv4, "22"));
}
