using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.Deletion;

internal sealed class FirewallBatchDeleteExecutor(IFirewallRuleSnapshotReader snapshotReader, IUfwRunner ufwRunner, ILogger logger) : IFirewallBatchDeleteExecutor
{
    private readonly ILogger<FirewallBatchDeleteExecutor> _logger = logger.Scoped<FirewallBatchDeleteExecutor>();

    public async Task<RuleBatchDeleteExecutionResult> ExecuteAsync(BatchDeleteRulesPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        RuleListResponse? baseline = await TryReadSnapshotAsync(cancellationToken);
        if (baseline is null)
        {
            return Result(RuleBatchDeleteExecutionOutcome.StateUncertain, null, payload.OccurrenceIds, "The current authoritative firewall state could not be read before batch deletion.");
        }

        string actualFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        if (!string.Equals(actualFingerprint, payload.BaselineFingerprint, StringComparison.Ordinal))
        {
            return Result(RuleBatchDeleteExecutionOutcome.StaleBaseline, baseline, payload.OccurrenceIds, "The authoritative firewall state no longer matches the signed batch-delete baseline.");
        }

        string? preconditionError = ValidatePreconditions(baseline, payload.OccurrenceIds);
        if (preconditionError is not null)
        {
            return Result(RuleBatchDeleteExecutionOutcome.PreconditionFailed, baseline, payload.OccurrenceIds, preconditionError);
        }

        int[] targets = [.. payload.OccurrenceIds.OrderDescending()];
        List<int> currentOrder = Enumerable.Range(0, baseline.Rules.Count).ToList();
        List<RuleBatchDeleteOperationReport> operations = [];
        RuleListResponse currentSnapshot = baseline;

        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int occurrenceId = targets[targetIndex];
            int[] pending = targets[targetIndex..];
            RuleListResponse? beforeDelete = await TryReadSnapshotAsync(cancellationToken);
            if (beforeDelete is null)
            {
                return new RuleBatchDeleteExecutionResult(
                    RuleBatchDeleteExecutionOutcome.StateUncertain,
                    null,
                    operations,
                    pending,
                    "The authoritative firewall state could not be read before the next batch-delete operation.");
            }
            if (!SnapshotMatchesOrder(beforeDelete, baseline, currentOrder))
            {
                RuleBatchDeleteExecutionOutcome outcome = operations.Count == 0 ? RuleBatchDeleteExecutionOutcome.StaleBaseline : RuleBatchDeleteExecutionOutcome.PartiallyCompleted;
                return new RuleBatchDeleteExecutionResult(outcome, beforeDelete, operations, pending, "Authoritative firewall state diverged before the next batch-delete operation.");
            }

            int currentIndex = currentOrder.IndexOf(occurrenceId);
            if (currentIndex < 0)
            {
                return new RuleBatchDeleteExecutionResult(
                    RuleBatchDeleteExecutionOutcome.PartiallyCompleted,
                    beforeDelete,
                    operations,
                    pending,
                    $"Batch-delete occurrence {occurrenceId} is no longer present in the expected state.");
            }

            ListedFirewallRule target = beforeDelete.Rules[currentIndex];
            if (target.DisplayNumber is not int displayNumber || displayNumber <= 0)
            {
                RuleBatchDeleteExecutionOutcome outcome = operations.Count == 0 ? RuleBatchDeleteExecutionOutcome.PreconditionFailed : RuleBatchDeleteExecutionOutcome.PartiallyCompleted;
                return new RuleBatchDeleteExecutionResult(outcome, beforeDelete, operations, pending, $"Batch-delete occurrence {occurrenceId} does not have a usable UFW display number.");
            }

            ProcessExecution process = await ExecuteProcessAsync(new UfwDeleteRuleCommand(displayNumber), cancellationToken);
            RuleListResponse? afterDelete = await TryReadSnapshotAsync(CancellationToken.None);
            List<int> expectedOrder = [.. currentOrder];
            expectedOrder.RemoveAt(currentIndex);

            if (afterDelete is not null && SnapshotMatchesOrder(afterDelete, baseline, expectedOrder))
            {
                RuleBatchDeleteOperationStatus status = process.Succeeded && !process.CancellationRequested
                    ? RuleBatchDeleteOperationStatus.Deleted
                    : RuleBatchDeleteOperationStatus.DeletedAfterProcessFailure;
                operations.Add(new RuleBatchDeleteOperationReport(occurrenceId, baseline.Rules[occurrenceId].RuleId, status, process.Diagnostic));
                currentOrder = expectedOrder;
                currentSnapshot = afterDelete;
                if (process.CancellationRequested || cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new OperationCanceledException("The UFW batch-delete subprocess was canceled after it started.", cancellationToken);
                }
                continue;
            }

            if (afterDelete is null)
            {
                string diagnostic = CombineDiagnostics(process.Diagnostic, $"The authoritative firewall state could not be confirmed after attempting to delete occurrence {occurrenceId}.")!;
                operations.Add(new RuleBatchDeleteOperationReport(occurrenceId, baseline.Rules[occurrenceId].RuleId, RuleBatchDeleteOperationStatus.StateUncertain, diagnostic));
                if (process.CancellationRequested || cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return new RuleBatchDeleteExecutionResult(RuleBatchDeleteExecutionOutcome.StateUncertain, null, operations, pending, diagnostic);
            }

            string failureDiagnostic = CombineDiagnostics(process.Diagnostic, $"Authoritative firewall state did not match the expected state after attempting to delete occurrence {occurrenceId}.")!;
            operations.Add(new RuleBatchDeleteOperationReport(occurrenceId, baseline.Rules[occurrenceId].RuleId, RuleBatchDeleteOperationStatus.Failed, failureDiagnostic));
            if (process.CancellationRequested || cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            return new RuleBatchDeleteExecutionResult(RuleBatchDeleteExecutionOutcome.PartiallyCompleted, afterDelete, operations, pending, failureDiagnostic);
        }

        return new RuleBatchDeleteExecutionResult(RuleBatchDeleteExecutionOutcome.Completed, currentSnapshot, operations, [], null);
    }

    private static string? ValidatePreconditions(RuleListResponse baseline, IReadOnlyList<int> occurrenceIds)
    {
        if (occurrenceIds.Count == 0)
        {
            return "At least one firewall-rule occurrence must be selected for batch deletion.";
        }

        HashSet<int> seen = [];
        foreach (int occurrenceId in occurrenceIds)
        {
            if (occurrenceId < 0 || occurrenceId >= baseline.Rules.Count)
            {
                return $"Batch-delete occurrence {occurrenceId} is outside the signed baseline snapshot.";
            }
            if (!seen.Add(occurrenceId))
            {
                return $"Batch-delete occurrence {occurrenceId} was requested more than once.";
            }
            if (baseline.Rules[occurrenceId].DisplayNumber is not int displayNumber || displayNumber <= 0)
            {
                return $"Batch-delete occurrence {occurrenceId} does not have a usable UFW display number.";
            }
        }
        return null;
    }

    private async Task<RuleListResponse?> TryReadSnapshotAsync(CancellationToken cancellationToken)
    {
        FirewallRuleSnapshotReadResult read = await snapshotReader.ReadAsync(cancellationToken);
        return read.Error is null ? FirewallRuleSet.ToListResponse(read.Snapshot!, read.Configuration!) : null;
    }

    private async Task<ProcessExecution> ExecuteProcessAsync(IUfwCommand command, CancellationToken cancellationToken)
    {
        try
        {
            UfwProcessResult result = await ufwRunner.ExecuteAsync(command, cancellationToken);
            string? diagnostic = result.Succeeded && !result.CancellationRequested ? null : FormatProcessDiagnostic(result);
            return new ProcessExecution(result.Succeeded, result.CancellationRequested, diagnostic);
        }
        catch (ChildProcessException exception)
        {
            _logger.LogError(exception, "Failed to start UFW while executing a batch-delete operation.");
            return new ProcessExecution(false, false, exception.Message);
        }
    }

    private static bool SnapshotMatchesOrder(RuleListResponse snapshot, RuleListResponse baseline, IReadOnlyList<int> expectedOrder)
    {
        if (snapshot.Active != baseline.Active || snapshot.Rules.Count != expectedOrder.Count)
        {
            return false;
        }
        for (int index = 0; index < expectedOrder.Count; index++)
        {
            if (!FirewallRuleSemanticComparer.Equals(snapshot.Rules[index], baseline.Rules[expectedOrder[index]]))
            {
                return false;
            }
        }
        return true;
    }

    private static string FormatProcessDiagnostic(UfwProcessResult result)
    {
        string details = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        if (result.CancellationRequested)
        {
            return "UFW process was canceled after it started.";
        }
        return $"UFW process exited with code {result.ExitCode}: {details}";
    }

    private static string? CombineDiagnostics(params string?[] diagnostics)
    {
        string[] nonEmpty = diagnostics.Where(static diagnostic => !string.IsNullOrWhiteSpace(diagnostic)).Select(static diagnostic => diagnostic!).ToArray();
        return nonEmpty.Length == 0 ? null : string.Join(' ', nonEmpty);
    }

    private static RuleBatchDeleteExecutionResult Result(
        RuleBatchDeleteExecutionOutcome outcome,
        RuleListResponse? finalSnapshot,
        IReadOnlyList<int> pendingOccurrenceIds,
        string? diagnostic = null) =>
        new(outcome, finalSnapshot, [], [.. pendingOccurrenceIds], diagnostic);

    private sealed record ProcessExecution(bool Succeeded, bool CancellationRequested, string? Diagnostic);
}
