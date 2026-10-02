using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallReorderExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IFirewallReorderPreflightEvaluator preflightEvaluator,
    IFirewallReorderMoveExecutor moveExecutor) : IFirewallReorderExecutor
{
    public async Task<RuleReorderExecutionResult> ExecuteAsync(RuleReorderExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BaselineFingerprint);
        ArgumentNullException.ThrowIfNull(request.DesiredOrder);

        RuleListResponse? baseline = await snapshotReader.ReadAsync(cancellationToken).OrDefaultAsync();
        if (baseline is null)
        {
            return Result(RuleReorderExecutionOutcome.StateUncertain, null, diagnostic: "The current authoritative firewall state could not be read before reordering.");
        }

        string actualFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        if (!string.Equals(actualFingerprint, request.BaselineFingerprint, StringComparison.Ordinal))
        {
            return Result(RuleReorderExecutionOutcome.StaleBaseline, baseline, diagnostic: "The authoritative firewall state no longer matches the signed reorder baseline.");
        }

        RuleReorderPreflight preflight;
        switch (preflightEvaluator.Evaluate(baseline, request.DesiredOrder))
        {
            case RuleReorderPreflightResult.Accepted accepted:
                preflight = accepted.Preflight;
                break;
            case RuleReorderPreflightResult.Rejected rejected:
                return Result(RuleReorderExecutionOutcome.PreconditionFailed, baseline, diagnostic: rejected.Diagnostic);
            default:
                throw new InvalidOperationException("Unsupported reorder preflight result.");
        }

        if (preflight.Plan.Moves.Count == 0)
        {
            return Result(RuleReorderExecutionOutcome.Completed, baseline);
        }

        List<RuleReorderOperationReport> operationReports = [];
        IReadOnlyList<int> currentOrder = Enumerable.Range(0, baseline.Rules.Count).ToArray();
        RuleListResponse currentSnapshot = baseline;

        for (int moveIndex = 0; moveIndex < preflight.Plan.Moves.Count; moveIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RuleReorderMove move = preflight.Plan.Moves[moveIndex];
            RuleReorderMoveExecutionResult moveResult = await moveExecutor.ExecuteAsync(move, baseline, currentOrder, preflight.MoveSpecifications[move.OccurrenceId], cancellationToken);

            if (moveResult is RuleReorderMoveExecutionResult.Completed completed)
            {
                operationReports.Add(completed.OperationReport);
                currentOrder = completed.ResultingOrder;
                currentSnapshot = completed.Snapshot;
                continue;
            }

            if (moveResult is not RuleReorderMoveExecutionResult.Interrupted interrupted)
            {
                throw new InvalidOperationException("Unsupported reorder move execution result.");
            }

            if (interrupted.OperationReport is not null)
            {
                operationReports.Add(interrupted.OperationReport);
            }

            IReadOnlyList<RuleReorderMove> blocked = preflight.Plan.Moves.Skip(moveIndex).ToArray();
            IReadOnlyList<RuleReorderMove> pending = preflightEvaluator.CreateSafePendingPlan(baseline, interrupted.Snapshot, preflight);
            RuleReorderExecutionOutcome outcome = interrupted.RecoveryFailed
                ? RuleReorderExecutionOutcome.RecoveryFailed
                : interrupted.Snapshot is null
                    ? RuleReorderExecutionOutcome.StateUncertain
                    : operationReports.Count == 0 && interrupted.OperationReport is null
                        ? RuleReorderExecutionOutcome.StaleBaseline
                        : RuleReorderExecutionOutcome.PartiallyCompleted;

            return new RuleReorderExecutionResult(outcome, interrupted.Snapshot, operationReports, blocked, pending, interrupted.Diagnostic);
        }

        return new RuleReorderExecutionResult(RuleReorderExecutionOutcome.Completed, currentSnapshot, operationReports, [], [], null);
    }

    private static RuleReorderExecutionResult Result(RuleReorderExecutionOutcome outcome, RuleListResponse? finalSnapshot, string? diagnostic = null) =>
        new(outcome, finalSnapshot, [], [], [], diagnostic);
}
