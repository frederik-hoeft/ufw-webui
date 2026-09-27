using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules.Replacement;

internal sealed record RuleEditWorkflowState
{
    private RuleEditWorkflowState(
        RuleReplacementMutationResponse? replacement,
        string? confirmedRuleId,
        string? metadataSaveDiagnostic,
        bool contextInvalidated)
    {
        Replacement = replacement;
        ConfirmedRuleId = confirmedRuleId;
        MetadataSaveDiagnostic = metadataSaveDiagnostic;
        ContextInvalidated = contextInvalidated;
    }

    public static RuleEditWorkflowState Initial { get; } = new(replacement: null, confirmedRuleId: null, metadataSaveDiagnostic: null, contextInvalidated: false);

    public RuleReplacementMutationResponse? Replacement { get; }

    public string? ConfirmedRuleId { get; }

    public string? MetadataSaveDiagnostic { get; }

    public bool ContextInvalidated { get; }

    public bool FirewallCompleted => Replacement?.Firewall.Outcome == RuleReplacementOutcome.Completed;

    public bool CanApplyMetadataEdits => FirewallCompleted && Replacement?.MetadataReconciliation == RuleReplacementMetadataReconciliationOutcome.Completed;

    public bool CanRetryMetadataSave => CanApplyMetadataEdits && !string.IsNullOrWhiteSpace(ConfirmedRuleId) && !string.IsNullOrWhiteSpace(MetadataSaveDiagnostic);

    public RuleEditWorkflowState ApplyReplacement(RuleReplacementMutationResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.Firewall);
        if (Replacement is not null || ContextInvalidated)
        {
            throw new InvalidOperationException("A rule replacement result can only be applied to an active edit context once.");
        }

        string? confirmedRuleId = null;
        if (response.Firewall.Outcome == RuleReplacementOutcome.Completed)
        {
            confirmedRuleId = GetConfirmedReplacementIdentity(response.Firewall);
        }

        return new RuleEditWorkflowState(response, confirmedRuleId, metadataSaveDiagnostic: null, contextInvalidated: true);
    }

    public RuleEditWorkflowState InvalidateContext() => ContextInvalidated
        ? this
        : new RuleEditWorkflowState(Replacement, ConfirmedRuleId, MetadataSaveDiagnostic, contextInvalidated: true);

    public RuleEditWorkflowState MetadataSaveFailed(string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        if (!CanApplyMetadataEdits || string.IsNullOrWhiteSpace(ConfirmedRuleId))
        {
            throw new InvalidOperationException("Metadata retry state requires a completed firewall replacement with successful automatic metadata reconciliation.");
        }

        return new RuleEditWorkflowState(Replacement, ConfirmedRuleId, diagnostic.Trim(), contextInvalidated: true);
    }

    public RuleEditWorkflowState MetadataSaveCompleted()
    {
        if (!CanApplyMetadataEdits)
        {
            throw new InvalidOperationException("Metadata completion requires a completed firewall replacement with successful automatic metadata reconciliation.");
        }

        return new RuleEditWorkflowState(Replacement, ConfirmedRuleId, metadataSaveDiagnostic: null, contextInvalidated: true);
    }

    private static string GetConfirmedReplacementIdentity(RuleReplacementResponse response)
    {
        if (response.ReplacementRule is not { RuleId: { } ruleId } || string.IsNullOrWhiteSpace(ruleId))
        {
            throw new ApiProtocolException("A completed rule replacement response did not contain a semantic replacement identity.");
        }

        return ruleId;
    }
}
