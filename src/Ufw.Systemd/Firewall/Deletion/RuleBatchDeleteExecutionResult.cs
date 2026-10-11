using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Deletion;

internal sealed record RuleBatchDeleteExecutionResult(
    RuleBatchDeleteExecutionOutcome Outcome,
    FirewallSnapshot? FinalSnapshot,
    IReadOnlyList<RuleBatchDeleteOperationReport> Operations,
    IReadOnlyList<int> PendingOccurrenceIds,
    string? Diagnostic);
