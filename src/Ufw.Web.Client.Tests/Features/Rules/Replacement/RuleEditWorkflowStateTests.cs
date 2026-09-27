using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Features.Rules.Replacement;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules.Replacement;

[TestClass]
public sealed class RuleEditWorkflowStateTests
{
    [TestMethod]
    public void ApplyReplacement_CompletedSameIdentityConsumesContextAndConfirmsOriginalIdentity()
    {
        RuleEditWorkflowState state = RuleEditWorkflowState.Initial.ApplyReplacement(Completed("same", RuleReplacementMetadataReconciliationOutcome.Completed));

        Assert.IsTrue(state.FirewallCompleted);
        Assert.IsTrue(state.ContextInvalidated);
        Assert.AreEqual("same", state.ConfirmedRuleId);
        Assert.IsTrue(state.CanApplyMetadataEdits);
        Assert.IsFalse(state.CanRetryMetadataSave);
    }

    [TestMethod]
    public void ApplyReplacement_CompletedIdentityChangeUsesDaemonConfirmedReplacementIdentity()
    {
        RuleEditWorkflowState state = RuleEditWorkflowState.Initial.ApplyReplacement(Completed("replacement", RuleReplacementMetadataReconciliationOutcome.Completed));

        Assert.IsTrue(state.FirewallCompleted);
        Assert.AreEqual("replacement", state.ConfirmedRuleId);
        Assert.IsTrue(state.CanApplyMetadataEdits);
    }

    [TestMethod]
    public void ApplyReplacement_MetadataReconciliationFailurePreservesFirewallSuccessButBlocksUserMetadataMutation()
    {
        RuleEditWorkflowState state = RuleEditWorkflowState.Initial.ApplyReplacement(Completed(
            "replacement",
            RuleReplacementMetadataReconciliationOutcome.Failed,
            metadataDiagnostic: "database unavailable"));

        Assert.IsTrue(state.FirewallCompleted);
        Assert.IsTrue(state.ContextInvalidated);
        Assert.AreEqual("replacement", state.ConfirmedRuleId);
        Assert.IsFalse(state.CanApplyMetadataEdits);
        Assert.IsFalse(state.CanRetryMetadataSave);
        Assert.AreEqual("database unavailable", state.Replacement!.MetadataDiagnostic);
    }

    [TestMethod]
    public void MetadataSaveFailure_PreservesCompletedFirewallStateAndAllowsOnlyMetadataRetry()
    {
        RuleEditWorkflowState completed = RuleEditWorkflowState.Initial.ApplyReplacement(Completed("replacement", RuleReplacementMetadataReconciliationOutcome.Completed));

        RuleEditWorkflowState failed = completed.MetadataSaveFailed("metadata write failed");

        Assert.IsTrue(failed.FirewallCompleted);
        Assert.IsTrue(failed.ContextInvalidated);
        Assert.AreEqual("replacement", failed.ConfirmedRuleId);
        Assert.AreEqual("metadata write failed", failed.MetadataSaveDiagnostic);
        Assert.IsTrue(failed.CanRetryMetadataSave);

        RuleEditWorkflowState retried = failed.MetadataSaveCompleted();
        Assert.IsTrue(retried.FirewallCompleted);
        Assert.IsNull(retried.MetadataSaveDiagnostic);
        Assert.IsFalse(retried.CanRetryMetadataSave);
    }

    [TestMethod]
    [DataRow(RuleReplacementOutcome.StaleBaseline)]
    [DataRow(RuleReplacementOutcome.PreconditionFailed)]
    [DataRow(RuleReplacementOutcome.PartiallyCompleted)]
    [DataRow(RuleReplacementOutcome.StateUncertain)]
    public void ApplyReplacement_NonCompletedOutcomeInvalidatesContextWithoutInventingConfirmedIdentity(RuleReplacementOutcome outcome)
    {
        RuleReplacementResponse firewall = new(outcome, Snapshot("original"), ReplacementRule: null, RecoveryOutcome: null, Diagnostic: "not completed");
        RuleReplacementMutationResponse response = new(firewall, RuleReplacementMetadataReconciliationOutcome.NotAttempted);

        RuleEditWorkflowState state = RuleEditWorkflowState.Initial.ApplyReplacement(response);

        Assert.IsFalse(state.FirewallCompleted);
        Assert.IsTrue(state.ContextInvalidated);
        Assert.IsNull(state.ConfirmedRuleId);
        Assert.IsFalse(state.CanApplyMetadataEdits);
        Assert.IsFalse(state.CanRetryMetadataSave);
        Assert.AreEqual(outcome, state.Replacement!.Firewall.Outcome);
    }

    [TestMethod]
    public void ApplyReplacement_RejectsCompletedResponseWithoutConfirmedSemanticIdentity()
    {
        ListedFirewallRule replacement = new()
        {
            DisplayNumber = 1,
            Parsed = true,
            RawLine = "replacement",
            Rule = Specification(),
        };
        RuleReplacementResponse firewall = new(RuleReplacementOutcome.Completed, Snapshot("replacement"), replacement, RecoveryOutcome: null, Diagnostic: null);
        RuleReplacementMutationResponse response = new(firewall, RuleReplacementMetadataReconciliationOutcome.Completed);

        Assert.ThrowsExactly<ApiProtocolException>(() => RuleEditWorkflowState.Initial.ApplyReplacement(response));
    }

    [TestMethod]
    public void ApplyReplacement_CannotReuseConsumedWorkflowState()
    {
        RuleReplacementMutationResponse response = Completed("replacement", RuleReplacementMetadataReconciliationOutcome.Completed);
        RuleEditWorkflowState completed = RuleEditWorkflowState.Initial.ApplyReplacement(response);

        Assert.ThrowsExactly<InvalidOperationException>(() => completed.ApplyReplacement(response));
        Assert.ThrowsExactly<InvalidOperationException>(() => completed.InvalidateContext().ApplyReplacement(response));
    }

    private static RuleReplacementMutationResponse Completed(
        string ruleId,
        RuleReplacementMetadataReconciliationOutcome metadataOutcome,
        string? metadataDiagnostic = null)
    {
        ListedFirewallRule replacement = Rule(ruleId);
        RuleReplacementResponse firewall = new(RuleReplacementOutcome.Completed, Snapshot(ruleId), replacement, RecoveryOutcome: null, Diagnostic: null);
        return new RuleReplacementMutationResponse(firewall, metadataOutcome, metadataDiagnostic);
    }

    private static RuleListResponse Snapshot(string ruleId) => new(Active: true, [Rule(ruleId)], TestFirewallConfiguration.Enabled);

    private static ListedFirewallRule Rule(string ruleId) => new()
    {
        RuleId = ruleId,
        DisplayNumber = 1,
        Parsed = true,
        RawLine = ruleId,
        Rule = Specification(),
    };

    private static FirewallRuleSpecification Specification() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
    };
}
