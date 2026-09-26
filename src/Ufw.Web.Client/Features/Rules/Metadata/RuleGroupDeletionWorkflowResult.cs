using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed record RuleGroupDeletionWorkflowResult(
    RuleGroupDeletionWorkflowOutcome Outcome,
    IReadOnlyList<RuleGroup> Groups,
    RuleBatchDeleteResponse? BatchResponse = null,
    string? CleanupDiagnostic = null);
