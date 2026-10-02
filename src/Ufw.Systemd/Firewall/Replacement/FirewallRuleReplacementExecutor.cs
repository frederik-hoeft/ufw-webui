using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.Replacement;

internal sealed class FirewallRuleReplacementExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IFirewallRuleInterfaceValidator interfaceValidator,
    IFirewallRuleCapabilityValidator capabilityValidator,
    IUfwRunner ufwRunner,
    IUfwRuleCommandRenderer renderer,
    ILogger logger) : IFirewallRuleReplacementExecutor
{
    private readonly ILogger<FirewallRuleReplacementExecutor> _logger = logger.Scoped<FirewallRuleReplacementExecutor>();

    public async Task<RuleReplacementExecutionResult> ExecuteAsync(ReplaceRulePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        RuleReplacementContract.ValidatePayload(payload);

        RuleListResponse? baseline = await snapshotReader.ReadAsync(cancellationToken).OrDefaultAsync();
        if (baseline is null)
        {
            return Result(RuleReplacementExecutionOutcome.StateUncertain, null, diagnostic: "The authoritative firewall state could not be read before rule replacement.");
        }
        if (!string.Equals(FirewallRuleSnapshotFingerprint.Compute(baseline), payload.BaselineFingerprint, StringComparison.Ordinal))
        {
            return Result(RuleReplacementExecutionOutcome.StaleBaseline, baseline, diagnostic: "The authoritative firewall state no longer matches the signed replacement baseline.");
        }

        ListedFirewallRule target;
        FirewallRuleSpecification replacement = RuleSpecificationNormalizer.Normalize(payload.ReplacementRule);
        try
        {
            target = RuleReplacementContract.ResolveTarget(baseline.Rules, payload);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result(RuleReplacementExecutionOutcome.PreconditionFailed, baseline, diagnostic: exception.Message);
        }

        FirewallRuleSpecification original = RuleSpecificationNormalizer.Normalize(target.Rule!);
        string replacementId = RuleIdentity.Compute(replacement);
        bool sameIdentity = string.Equals(replacementId, payload.OriginalRuleId, StringComparison.Ordinal);
        int originalMultiplicity = baseline.Rules.Count(rule => string.Equals(rule.RuleId, payload.OriginalRuleId, StringComparison.Ordinal));

        if (sameIdentity && originalMultiplicity != 1)
        {
            return Result(
                RuleReplacementExecutionOutcome.PreconditionFailed,
                baseline,
                diagnostic: "The target semantic rule identity occurs more than once. UFW cannot safely apply an occurrence-specific existing-rule update while duplicates are present.");
        }
        if (sameIdentity && FirewallRuleStateComparer.Equals(original, replacement))
        {
            return new RuleReplacementExecutionResult(RuleReplacementExecutionOutcome.Completed, baseline, target, null, null);
        }
        if (!sameIdentity && baseline.Rules.Any(rule => string.Equals(rule.RuleId, replacementId, StringComparison.Ordinal)))
        {
            return Result(RuleReplacementExecutionOutcome.PreconditionFailed, baseline, diagnostic: "The replacement would duplicate an existing semantic firewall rule.");
        }

        IResponsePayload? capabilityError = capabilityValidator.Validate(replacement, baseline.Configuration);
        if (capabilityError is not null)
        {
            return Result(RuleReplacementExecutionOutcome.PreconditionFailed, baseline, diagnostic: GetResponseDiagnostic(capabilityError));
        }

        IResponsePayload? interfaceError = interfaceValidator.Validate(replacement);
        if (interfaceError is not null)
        {
            RuleReplacementExecutionOutcome outcome = interfaceError is ModelValidationErrorResponse
                ? RuleReplacementExecutionOutcome.PreconditionFailed
                : RuleReplacementExecutionOutcome.StateUncertain;
            return Result(outcome, baseline, diagnostic: GetResponseDiagnostic(interfaceError));
        }

        return sameIdentity
            ? await UpdateExistingRuleAsync(baseline, payload.TargetOccurrenceId, replacement, cancellationToken)
            : await ReplaceIdentityAsync(baseline, payload.TargetOccurrenceId, replacement, replacementId, cancellationToken);
    }

    private async Task<RuleReplacementExecutionResult> UpdateExistingRuleAsync(
        RuleListResponse baseline,
        int targetOccurrenceId,
        FirewallRuleSpecification replacement,
        CancellationToken cancellationToken)
    {
        ProcessExecution update = await ExecuteProcessAsync(new UfwUpdateExistingRuleCommand(replacement, renderer), "updating the existing rule", cancellationToken);
        RuleListResponse? finalSnapshot = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        if (finalSnapshot is null)
        {
            return Result(RuleReplacementExecutionOutcome.StateUncertain, null, diagnostic: CombineDiagnostics(update.Diagnostic, "The firewall state could not be read after updating the existing rule."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleReplacement(baseline, finalSnapshot, replacement, targetOccurrenceId, out ListedFirewallRule? replacementRule))
        {
            _logger.LogInformation($"Updated firewall rule '{replacementRule!.RuleId}' at signed snapshot occurrence {targetOccurrenceId}.");
            return new RuleReplacementExecutionResult(RuleReplacementExecutionOutcome.Completed, finalSnapshot, replacementRule, null, update.Succeeded ? null : update.Diagnostic);
        }

        if (FirewallRuleSnapshotMatcher.Equivalent(baseline, finalSnapshot))
        {
            ThrowIfCanceledAfterConfirmedSafeState(update, cancellationToken);
            return Result(RuleReplacementExecutionOutcome.PreconditionFailed, finalSnapshot, diagnostic: CombineDiagnostics(update.Diagnostic, "The existing rule was not updated."));
        }

        return Result(
            RuleReplacementExecutionOutcome.StateUncertain,
            finalSnapshot,
            diagnostic: CombineDiagnostics(update.Diagnostic, "Firewall state diverged from the exact existing-rule update postcondition."));
    }

    private async Task<RuleReplacementExecutionResult> ReplaceIdentityAsync(
        RuleListResponse baseline,
        int targetOccurrenceId,
        FirewallRuleSpecification replacement,
        string replacementId,
        CancellationToken cancellationToken)
    {
        int insertPosition = UfwRulePositionResolver.GetUfwInsertPosition(baseline.Rules, targetOccurrenceId);
        ProcessExecution insert = await ExecuteProcessAsync(new UfwInsertRuleCommand(insertPosition, replacement, renderer), "inserting the replacement rule", cancellationToken);
        RuleListResponse? afterInsert = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        if (afterInsert is null)
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                null,
                diagnostic: CombineDiagnostics(insert.Diagnostic, "The firewall state could not be read after inserting the replacement rule."));
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
                return Result(RuleReplacementExecutionOutcome.PreconditionFailed, afterInsert, diagnostic: CombineDiagnostics(insert.Diagnostic, "The replacement rule was not inserted."));
            }

            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                afterInsert,
                diagnostic: CombineDiagnostics(insert.Diagnostic, "Firewall state diverged from the exact replacement-insertion intermediate state."));
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
                CombineDiagnostics(insert.Diagnostic, "The original rule does not have a usable UFW display number after replacement insertion."));
        }

        ProcessExecution delete = await ExecuteProcessAsync(new UfwDeleteRuleCommand(oldDisplayNumber), "deleting the original rule", cancellationToken);
        RuleListResponse? afterDelete = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        string? mutationDiagnostic = CombineDiagnostics(insert.Succeeded ? null : insert.Diagnostic, delete.Diagnostic);
        if (afterDelete is null)
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                null,
                diagnostic: CombineDiagnostics(mutationDiagnostic, "The firewall state could not be read after deleting the original rule, so rollback was not attempted."));
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
                diagnostic: CombineDiagnostics(mutationDiagnostic, "The replacement operation left the original firewall state unchanged."));
        }

        if (!FirewallRuleSnapshotMatcher.TryMatchSingleInsertion(baseline, afterDelete, replacement, targetOccurrenceId, out _))
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                afterDelete,
                diagnostic: CombineDiagnostics(mutationDiagnostic, "Firewall state diverged after attempting to delete the original rule, so rollback was not attempted."));
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
                diagnostic: CombineDiagnostics(operationDiagnostic, "The inserted replacement does not have a usable UFW display number for rollback."));
        }

        ProcessExecution rollback = await ExecuteProcessAsync(new UfwDeleteRuleCommand(displayNumber), "rolling back the inserted replacement", CancellationToken.None);
        RuleListResponse? afterRollback = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        string? diagnostic = CombineDiagnostics(operationDiagnostic, rollback.Diagnostic);
        if (afterRollback is null)
        {
            return Result(
                RuleReplacementExecutionOutcome.StateUncertain,
                null,
                recoveryStatus: RuleReplacementRecoveryStatus.Failed,
                diagnostic: CombineDiagnostics(diagnostic, "The firewall state could not be read after attempting replacement rollback."));
        }

        if (FirewallRuleSnapshotMatcher.Equivalent(baseline, afterRollback))
        {
            return Result(
                RuleReplacementExecutionOutcome.PreconditionFailed,
                afterRollback,
                recoveryStatus: RuleReplacementRecoveryStatus.RestoredBaseline,
                diagnostic: CombineDiagnostics(diagnostic, "The replacement did not complete and the original firewall state was restored."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleReplacement(baseline, afterRollback, replacement, targetOccurrenceId, out ListedFirewallRule? replacementRule))
        {
            return new RuleReplacementExecutionResult(
                RuleReplacementExecutionOutcome.Completed,
                afterRollback,
                replacementRule,
                RuleReplacementRecoveryStatus.Failed,
                CombineDiagnostics(diagnostic, "The requested final replacement state was nevertheless confirmed after the rollback attempt."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleInsertion(baseline, afterRollback, replacement, targetOccurrenceId, out _))
        {
            return Result(
                RuleReplacementExecutionOutcome.PartiallyCompleted,
                afterRollback,
                recoveryStatus: RuleReplacementRecoveryStatus.Failed,
                diagnostic: CombineDiagnostics(diagnostic, "Rollback did not remove the inserted replacement; both original and replacement rules remain present."));
        }

        return Result(
            RuleReplacementExecutionOutcome.StateUncertain,
            afterRollback,
            recoveryStatus: RuleReplacementRecoveryStatus.Failed,
            diagnostic: CombineDiagnostics(diagnostic, "Firewall state diverged from every safe replacement or rollback postcondition."));
    }

    private async Task<ProcessExecution> ExecuteProcessAsync(IUfwCommand command, string operation, CancellationToken cancellationToken)
    {
        try
        {
            UfwProcessResult result = await ufwRunner.ExecuteAsync(command, cancellationToken);
            string? diagnostic = result.Succeeded ? null : FormatProcessDiagnostic(result, operation);
            return new ProcessExecution(result.Succeeded, result.CancellationRequested, diagnostic);
        }
        catch (ChildProcessException exception)
        {
            _logger.LogError(exception, $"UFW execution failed while {operation}. Authoritative state will be reconciled before classifying the replacement result.");
            return new ProcessExecution(false, false, exception.Message);
        }
    }

    private static void ThrowIfCanceledAfterConfirmedSafeState(ProcessExecution process, CancellationToken cancellationToken)
    {
        if (process.CancellationRequested || cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException("The UFW replacement subprocess was canceled after it started.", cancellationToken);
        }
    }

    private static string GetResponseDiagnostic(IResponsePayload response) => response switch
    {
        ErrorResponse error => error.Message ?? "Rule replacement precondition validation failed.",
        _ => "Rule replacement precondition validation failed.",
    };

    private static string FormatProcessDiagnostic(UfwProcessResult result, string operation)
    {
        string details = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return result.CancellationRequested
            ? $"UFW process was canceled after it started while {operation}."
            : $"UFW process exited with code {result.ExitCode} while {operation}: {details}";
    }

    private static string? CombineDiagnostics(params string?[] diagnostics)
    {
        string[] nonEmpty = diagnostics.Where(static diagnostic => !string.IsNullOrWhiteSpace(diagnostic)).Select(static diagnostic => diagnostic!).ToArray();
        return nonEmpty.Length == 0 ? null : string.Join(' ', nonEmpty);
    }

    private static RuleReplacementExecutionResult Result(
        RuleReplacementExecutionOutcome outcome,
        RuleListResponse? finalSnapshot,
        ListedFirewallRule? replacementRule = null,
        RuleReplacementRecoveryStatus? recoveryStatus = null,
        string? diagnostic = null) =>
        new(outcome, finalSnapshot, replacementRule, recoveryStatus, diagnostic);

    private sealed record ProcessExecution(bool Succeeded, bool CancellationRequested, string? Diagnostic);
}
