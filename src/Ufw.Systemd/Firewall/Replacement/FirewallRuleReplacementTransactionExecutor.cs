using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.Replacement;

internal sealed class FirewallRuleReplacementTransactionExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IUfwProcessExecutor processExecutor,
    IUfwRuleCommandRenderer renderer,
    ILogger logger) : IFirewallRuleReplacementTransactionExecutor
{
    private readonly ILogger<FirewallRuleReplacementTransactionExecutor> _logger = logger.Scoped<FirewallRuleReplacementTransactionExecutor>();

    public Task<RuleReplacementExecutionResult> ExecuteAsync(RuleListResponse baseline, RuleReplacementPreflight preflight, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(preflight);

        return preflight.TransactionKind switch
        {
            RuleReplacementTransactionKind.UpdateExisting => UpdateExistingRuleAsync(baseline, preflight.TargetOccurrenceId, preflight.Replacement, cancellationToken),
            RuleReplacementTransactionKind.InsertThenDelete => ReplaceIdentityAsync(baseline, preflight.TargetOccurrenceId, preflight.Replacement, preflight.ReplacementId, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported replacement transaction kind '{preflight.TransactionKind}'."),
        };
    }

    private async Task<RuleReplacementExecutionResult> UpdateExistingRuleAsync(
        RuleListResponse baseline,
        int targetOccurrenceId,
        FirewallRuleSpecification replacement,
        CancellationToken cancellationToken)
    {
        UfwProcessExecutionResult update = await processExecutor.ExecuteAsync(new UfwUpdateExistingRuleCommand(replacement, renderer), "updating the existing rule", cancellationToken);
        RuleListResponse? finalSnapshot = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        if (finalSnapshot is null)
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                null,
                diagnostic: UfwProcessDiagnostics.Combine(update.Diagnostic, "The firewall state could not be read after updating the existing rule."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleReplacement(baseline, finalSnapshot, replacement, targetOccurrenceId, out ListedFirewallRule? replacementRule))
        {
            _logger.LogInformation($"Updated firewall rule '{replacementRule!.RuleId}' at signed snapshot occurrence {targetOccurrenceId}.");
            return new RuleReplacementExecutionResult(RuleReplacementExecutionOutcome.Completed, finalSnapshot, replacementRule, null, update.Succeeded ? null : update.Diagnostic);
        }

        if (FirewallRuleSnapshotMatcher.Equivalent(baseline, finalSnapshot))
        {
            ThrowIfCanceledAfterConfirmedSafeState(update, cancellationToken);
            return Result(RuleReplacementExecutionOutcome.PreconditionFailed, finalSnapshot, diagnostic: UfwProcessDiagnostics.Combine(update.Diagnostic, "The existing rule was not updated."));
        }

        return Result(
            RuleReplacementExecutionOutcome.StateUncertain,
            finalSnapshot,
            diagnostic: UfwProcessDiagnostics.Combine(update.Diagnostic, "Firewall state diverged from the exact existing-rule update postcondition."));
    }

    private async Task<RuleReplacementExecutionResult> ReplaceIdentityAsync(
        RuleListResponse baseline,
        int targetOccurrenceId,
        FirewallRuleSpecification replacement,
        string replacementId,
        CancellationToken cancellationToken)
    {
        UfwInsertionPlacement placement = UfwInsertionPlacementResolver.Resolve(baseline.Rules, replacement.AddressFamily, targetOccurrenceId);
        UfwProcessExecutionResult insert = await processExecutor.ExecuteAsync(placement.CreateCommand(replacement, renderer), "inserting the replacement rule", cancellationToken);
        RuleListResponse? afterInsert = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        if (afterInsert is null)
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                null,
                diagnostic: UfwProcessDiagnostics.Combine(insert.Diagnostic, "The firewall state could not be read after inserting the replacement rule."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleReplacement(baseline, afterInsert, replacement, targetOccurrenceId, out ListedFirewallRule? completedWithoutDelete))
        {
            _logger.LogInformation($"Firewall rule replacement '{replacementId}' reached the exact final state while reconciling insertion.");
            return new RuleReplacementExecutionResult(RuleReplacementExecutionOutcome.Completed, afterInsert, completedWithoutDelete, null, insert.Succeeded ? null : insert.Diagnostic);
        }

        if (!FirewallRuleSnapshotMatcher.TryMatchSingleInsertion(baseline, afterInsert, replacement, targetOccurrenceId, out _))
        {
            if (FirewallRuleSnapshotMatcher.Equivalent(baseline, afterInsert))
            {
                ThrowIfCanceledAfterConfirmedSafeState(insert, cancellationToken);
                return Result(RuleReplacementExecutionOutcome.PreconditionFailed, afterInsert, diagnostic: UfwProcessDiagnostics.Combine(insert.Diagnostic, "The replacement rule was not inserted."));
            }

            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                afterInsert,
                diagnostic: UfwProcessDiagnostics.Combine(insert.Diagnostic, "Firewall state diverged from the exact replacement-insertion intermediate state."));
        }

        if (insert.CancellationRequested || cancellationToken.IsCancellationRequested)
        {
            RuleReplacementExecutionResult canceledRecovery = await RollBackInsertionAsync(baseline, afterInsert, targetOccurrenceId, replacement, insert.Diagnostic);
            if (canceledRecovery.Outcome == RuleReplacementExecutionOutcome.PreconditionFailed && canceledRecovery.RecoveryStatus == RuleReplacementRecoveryStatus.RestoredBaseline)
            {
                ThrowIfCanceledAfterConfirmedSafeState(insert, cancellationToken);
            }
            return canceledRecovery;
        }

        int oldOccurrenceIndex = targetOccurrenceId + 1;
        ListedFirewallRule oldRule = afterInsert.Rules[oldOccurrenceIndex];
        if (oldRule.DisplayNumber is not int oldDisplayNumber || oldDisplayNumber <= 0)
        {
            return await RollBackInsertionAsync(
                baseline,
                afterInsert,
                targetOccurrenceId,
                replacement,
                UfwProcessDiagnostics.Combine(insert.Diagnostic, "The original rule does not have a usable UFW display number after replacement insertion."));
        }

        UfwProcessExecutionResult delete = await processExecutor.ExecuteAsync(new UfwDeleteRuleCommand(oldDisplayNumber), "deleting the original rule", cancellationToken);
        RuleListResponse? afterDelete = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        string? mutationDiagnostic = UfwProcessDiagnostics.Combine(insert.Succeeded ? null : insert.Diagnostic, delete.Diagnostic);
        if (afterDelete is null)
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                null,
                diagnostic: UfwProcessDiagnostics.Combine(mutationDiagnostic, "The firewall state could not be read after deleting the original rule, so rollback was not attempted."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleReplacement(baseline, afterDelete, replacement, targetOccurrenceId, out ListedFirewallRule? replacementRule))
        {
            _logger.LogInformation($"Replaced firewall rule at signed snapshot occurrence {targetOccurrenceId} with semantic identity '{replacementId}'.");
            return new RuleReplacementExecutionResult(RuleReplacementExecutionOutcome.Completed, afterDelete, replacementRule, null, mutationDiagnostic);
        }

        if (FirewallRuleSnapshotMatcher.Equivalent(baseline, afterDelete))
        {
            ThrowIfCanceledAfterConfirmedSafeState(delete, cancellationToken);
            return Result(
                RuleReplacementExecutionOutcome.PreconditionFailed,
                afterDelete,
                diagnostic: UfwProcessDiagnostics.Combine(mutationDiagnostic, "The replacement operation left the original firewall state unchanged."));
        }

        if (!FirewallRuleSnapshotMatcher.TryMatchSingleInsertion(baseline, afterDelete, replacement, targetOccurrenceId, out _))
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                afterDelete,
                diagnostic: UfwProcessDiagnostics.Combine(mutationDiagnostic, "Firewall state diverged after attempting to delete the original rule, so rollback was not attempted."));
        }

        RuleReplacementExecutionResult recovery = await RollBackInsertionAsync(baseline, afterDelete, targetOccurrenceId, replacement, mutationDiagnostic);
        if (recovery.Outcome == RuleReplacementExecutionOutcome.PreconditionFailed
            && recovery.RecoveryStatus == RuleReplacementRecoveryStatus.RestoredBaseline)
        {
            ThrowIfCanceledAfterConfirmedSafeState(delete, cancellationToken);
        }
        return recovery;
    }

    private async Task<RuleReplacementExecutionResult> RollBackInsertionAsync(
        RuleListResponse baseline,
        RuleListResponse exactIntermediate,
        int targetOccurrenceId,
        FirewallRuleSpecification replacement,
        string? operationDiagnostic)
    {
        ListedFirewallRule inserted = exactIntermediate.Rules[targetOccurrenceId];
        if (inserted.DisplayNumber is not int displayNumber || displayNumber <= 0)
        {
            return Result(
                RuleReplacementExecutionOutcome.PartiallyCompleted,
                exactIntermediate,
                recoveryStatus: RuleReplacementRecoveryStatus.Failed,
                diagnostic: UfwProcessDiagnostics.Combine(operationDiagnostic, "The inserted replacement does not have a usable UFW display number for rollback."));
        }

        UfwProcessExecutionResult rollback = await processExecutor.ExecuteAsync(new UfwDeleteRuleCommand(displayNumber), "rolling back the inserted replacement", CancellationToken.None);
        RuleListResponse? afterRollback = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        string? diagnostic = UfwProcessDiagnostics.Combine(operationDiagnostic, rollback.Diagnostic);
        if (afterRollback is null)
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                null,
                recoveryStatus: RuleReplacementRecoveryStatus.Failed,
                diagnostic: UfwProcessDiagnostics.Combine(diagnostic, "The firewall state could not be read after attempting replacement rollback."));
        }

        if (FirewallRuleSnapshotMatcher.Equivalent(baseline, afterRollback))
        {
            return Result(
                RuleReplacementExecutionOutcome.PreconditionFailed,
                afterRollback,
                recoveryStatus: RuleReplacementRecoveryStatus.RestoredBaseline,
                diagnostic: UfwProcessDiagnostics.Combine(diagnostic, "The replacement did not complete and the original firewall state was restored."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleReplacement(baseline, afterRollback, replacement, targetOccurrenceId, out ListedFirewallRule? replacementRule))
        {
            return new RuleReplacementExecutionResult(
                RuleReplacementExecutionOutcome.Completed,
                afterRollback,
                replacementRule,
                RuleReplacementRecoveryStatus.Failed,
                UfwProcessDiagnostics.Combine(diagnostic, "The requested final replacement state was nevertheless confirmed after the rollback attempt."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleInsertion(baseline, afterRollback, replacement, targetOccurrenceId, out _))
        {
            return Result(
                RuleReplacementExecutionOutcome.PartiallyCompleted,
                afterRollback,
                recoveryStatus: RuleReplacementRecoveryStatus.Failed,
                diagnostic: UfwProcessDiagnostics.Combine(diagnostic, "Rollback did not remove the inserted replacement; both original and replacement rules remain present."));
        }

        return Result(
            RuleReplacementExecutionOutcome.StateUncertain,
            afterRollback,
            recoveryStatus: RuleReplacementRecoveryStatus.Failed,
            diagnostic: UfwProcessDiagnostics.Combine(diagnostic, "Firewall state diverged from every safe replacement or rollback postcondition."));
    }

    private static void ThrowIfCanceledAfterConfirmedSafeState(UfwProcessExecutionResult process, CancellationToken cancellationToken)
    {
        if (process.CancellationRequested || cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException("The UFW replacement subprocess was canceled after it started.", cancellationToken);
        }
    }

    private static RuleReplacementExecutionResult Result(
        RuleReplacementExecutionOutcome outcome,
        RuleListResponse? finalSnapshot,
        ListedFirewallRule? replacementRule = null,
        RuleReplacementRecoveryStatus? recoveryStatus = null,
        string? diagnostic = null) =>
        new(outcome, finalSnapshot, replacementRule, recoveryStatus, diagnostic);
}
