using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Replacement;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Replacement;

[TestClass]
public sealed class FirewallRuleReplacementPreflightEvaluatorTests
{
    [TestMethod]
    public void Evaluate_SameIdentityStateChange_SelectsExistingRuleUpdate()
    {
        FirewallRuleSpecification original = Rule("80", "old");
        FirewallSnapshot baseline = Baseline(original);
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("80", "new"));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator();

        RuleReplacementPreflightResult result = evaluator.Evaluate(baseline, payload);

        RuleReplacementPreflightResult.Ready ready = Assert.IsInstanceOfType<RuleReplacementPreflightResult.Ready>(result);
        Assert.AreEqual(RuleReplacementTransactionKind.UpdateExisting, ready.Preflight.TransactionKind);
        Assert.AreEqual("new", ready.Preflight.Replacement.Comment);
    }

    [TestMethod]
    public void Evaluate_IdentityChange_SelectsInsertThenDelete()
    {
        FirewallSnapshot baseline = Baseline(Rule("80"), Rule("443"));
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("22"));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator();

        RuleReplacementPreflightResult result = evaluator.Evaluate(baseline, payload);

        RuleReplacementPreflightResult.Ready ready = Assert.IsInstanceOfType<RuleReplacementPreflightResult.Ready>(result);
        Assert.AreEqual(RuleReplacementTransactionKind.InsertThenDelete, ready.Preflight.TransactionKind);
        Assert.AreEqual(RuleIdentity.Compute(payload.ReplacementRule), ready.Preflight.ReplacementId);
    }

    [TestMethod]
    public void Evaluate_UnchangedRule_ReturnsNoChange()
    {
        FirewallRuleSpecification rule = Rule("80", "same");
        FirewallSnapshot baseline = Baseline(rule);
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("80", "same"));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator();

        RuleReplacementPreflightResult result = evaluator.Evaluate(baseline, payload);

        RuleReplacementPreflightResult.NoChange noChange = Assert.IsInstanceOfType<RuleReplacementPreflightResult.NoChange>(result);
        Assert.AreSame(baseline.Rules[0], noChange.Target);
    }

    [TestMethod]
    public void Evaluate_TargetIdentityMismatch_ReturnsPreconditionFailure()
    {
        FirewallSnapshot baseline = Baseline(Rule("80"));
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("22"));
        payload.OriginalRuleId = RuleIdentity.Compute(Rule("9999"));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator();

        RuleReplacementPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReplacementPreflightResult.Rejected>(evaluator.Evaluate(baseline, payload));

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, rejected.Outcome);
        Assert.Contains("identity does not match", rejected.Diagnostic);
    }

    [TestMethod]
    public void Evaluate_DuplicateSameIdentity_ReturnsPreconditionFailure()
    {
        FirewallSnapshot baseline = Baseline(Rule("80", "first"), Rule("80", "second"));
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("80", "updated"));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator();

        RuleReplacementPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReplacementPreflightResult.Rejected>(evaluator.Evaluate(baseline, payload));

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, rejected.Outcome);
        Assert.Contains("occurs more than once", rejected.Diagnostic);
    }

    [TestMethod]
    public void Evaluate_DuplicateReplacementIdentity_ReturnsPreconditionFailure()
    {
        FirewallSnapshot baseline = Baseline(Rule("80"), Rule("22"));
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("22"));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator();

        RuleReplacementPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReplacementPreflightResult.Rejected>(evaluator.Evaluate(baseline, payload));

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, rejected.Outcome);
        Assert.Contains("duplicate", rejected.Diagnostic);
    }

    [TestMethod]
    public void Evaluate_DisabledIpv6Capability_ReturnsPreconditionFailure()
    {
        FirewallSnapshot baseline = Baseline(TestFirewallConfiguration.Disabled, Rule("80", family: FirewallAddressFamily.IPv6));
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("22", family: FirewallAddressFamily.IPv6));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator();

        RuleReplacementPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReplacementPreflightResult.Rejected>(evaluator.Evaluate(baseline, payload));

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, rejected.Outcome);
    }

    [TestMethod]
    public void Evaluate_InterfaceModelValidationFailure_ReturnsPreconditionFailure()
    {
        ModelValidationErrorResponse error = new([new ModelValidationError(nameof(FirewallRuleSpecification.SourceInterface), "missing")]);
        FirewallSnapshot baseline = Baseline(Rule("80"));
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("22"));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator(error);

        RuleReplacementPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReplacementPreflightResult.Rejected>(evaluator.Evaluate(baseline, payload));

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, rejected.Outcome);
    }

    [TestMethod]
    public void Evaluate_InterfaceInspectionFailure_ReturnsStateUncertain()
    {
        InternalServerErrorResponse error = new("interface inspection failed");
        FirewallSnapshot baseline = Baseline(Rule("80"));
        ReplaceRulePayload payload = Payload(baseline, 0, Rule("22"));
        FirewallRuleReplacementPreflightEvaluator evaluator = CreateEvaluator(error);

        RuleReplacementPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReplacementPreflightResult.Rejected>(evaluator.Evaluate(baseline, payload));

        Assert.AreEqual(RuleReplacementExecutionOutcome.StateUncertain, rejected.Outcome);
        Assert.Contains("interface inspection failed", rejected.Diagnostic);
    }

    private static FirewallRuleReplacementPreflightEvaluator CreateEvaluator(IResponsePayload? interfaceResponse = null)
    {
        Mock<IFirewallRuleInterfaceValidator> interfaceValidator = new(MockBehavior.Strict);
        interfaceValidator.Setup(validator => validator.Validate(It.IsAny<FirewallRuleSpecification>())).Returns(interfaceResponse);
        return new FirewallRuleReplacementPreflightEvaluator(interfaceValidator.Object, new FirewallRuleCapabilityValidator());
    }

    private static ReplaceRulePayload Payload(FirewallSnapshot baseline, int targetOccurrenceId, FirewallRuleSpecification replacement) => new()
    {
        BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline.Active, baseline.Rules),
        TargetOccurrenceId = targetOccurrenceId,
        OriginalRuleId = baseline.Rules[targetOccurrenceId].RuleId!,
        ReplacementRule = replacement,
    };

    private static FirewallSnapshot Baseline(params FirewallRuleSpecification[] rules) => Baseline(TestFirewallConfiguration.Enabled, rules);

    private static FirewallSnapshot Baseline(FirewallConfigurationSnapshot configuration, params FirewallRuleSpecification[] rules) =>
        new(true, rules.Select(ListedRule).ToArray(), configuration);

    private static ListedFirewallRule ListedRule(FirewallRuleSpecification rule) => new()
    {
        Parsed = true,
        RuleId = RuleIdentity.Compute(rule),
        Rule = rule,
    };

    private static FirewallRuleSpecification Rule(string port, string? comment = null, FirewallAddressFamily family = FirewallAddressFamily.IPv4) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = family,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        Destination = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        DestinationPorts = port,
        Comment = comment,
    };
}
