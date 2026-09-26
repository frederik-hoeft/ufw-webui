using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Deletion;

internal sealed record RuleBatchDeleteExecutionResult(
    RuleBatchDeleteExecutionOutcome Outcome,
    RuleListResponse? FinalSnapshot,
    IReadOnlyList<RuleBatchDeleteOperationReport> Operations,
    IReadOnlyList<int> PendingOccurrenceIds,
    string? Diagnostic);
