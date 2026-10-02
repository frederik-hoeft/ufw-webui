using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal abstract record RuleReorderMoveExecutionResult
{
    private RuleReorderMoveExecutionResult()
    {
    }

    internal sealed record Completed : RuleReorderMoveExecutionResult
    {
        public Completed(RuleListResponse snapshot, IReadOnlyList<int> resultingOrder, RuleReorderOperationReport operationReport)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(resultingOrder);
            ArgumentNullException.ThrowIfNull(operationReport);
            Snapshot = snapshot;
            ResultingOrder = resultingOrder;
            OperationReport = operationReport;
        }

        public RuleListResponse Snapshot { get; }

        public IReadOnlyList<int> ResultingOrder { get; }

        public RuleReorderOperationReport OperationReport { get; }
    }

    internal sealed record Interrupted(
        RuleListResponse? Snapshot,
        RuleReorderOperationReport? OperationReport,
        string? Diagnostic,
        bool RecoveryFailed) : RuleReorderMoveExecutionResult;
}
