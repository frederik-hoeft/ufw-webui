using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record RuleReorderExecutionResult(
    RuleReorderExecutionOutcome Outcome,
    FirewallSnapshot? FinalSnapshot,
    IReadOnlyList<RuleReorderOperationReport> Operations,
    IReadOnlyList<RuleReorderMove> BlockedOperations,
    IReadOnlyList<RuleReorderMove> PendingOperations,
    string? Diagnostic)
{
    public bool Complete => Outcome == RuleReorderExecutionOutcome.Completed;
}
