using Ufw.Shared.Extensions;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallReorderMoveExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IRuleReorderRecoveryCoordinator recoveryCoordinator,
    IReorderRecoveryJournal recoveryJournal,
    IUfwProcessExecutor processExecutor,
    IUfwRuleCommandRenderer renderer,
    ILogger logger) : IFirewallReorderMoveExecutor
{
    private readonly ILogger<FirewallReorderMoveExecutor> _logger = logger.Scoped<FirewallReorderMoveExecutor>();

    public async Task<RuleReorderMoveExecutionResult> ExecuteAsync(
        RuleReorderMove move,
        RuleListResponse baseline,
        IReadOnlyList<int> preMoveOrder,
        FirewallRuleSpecification specification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(preMoveOrder);
        ArgumentNullException.ThrowIfNull(specification);

        RuleListResponse? preMoveSnapshot = await snapshotReader.ReadAsync(cancellationToken).OrDefaultAsync();
        if (preMoveSnapshot is null)
        {
            return Interrupted(null, null, "The authoritative firewall state could not be read before the next move started.");
        }

        if (!FirewallRuleSnapshotMatcher.MatchesOrder(preMoveSnapshot, baseline, preMoveOrder))
        {
            return Interrupted(preMoveSnapshot, null, "Authoritative state diverged before the next move started.");
        }

        int currentIndex = preMoveOrder.IndexOf(move.OccurrenceId);
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Occurrence {move.OccurrenceId} is missing from the expected ordering state.");
        }
        ListedFirewallRule listedRule = preMoveSnapshot.Rules[currentIndex];
        if (listedRule.DisplayNumber is not int displayNumber)
        {
            return Interrupted(preMoveSnapshot, null, "The move target no longer has a usable UFW rule number.");
        }

        int originalFamilyPosition = UfwInsertionPlacementResolver.GetFamilyPosition(preMoveSnapshot.Rules, currentIndex);
        ReorderRecoveryJournalEntry journalEntry = CreateJournalEntry(specification, originalFamilyPosition, baseline, preMoveOrder, currentIndex);
        await recoveryJournal.WriteAsync(journalEntry, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            await recoveryJournal.ClearAsync(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
        }

        UfwProcessExecutionResult delete;
        try
        {
            delete = await processExecutor.ExecuteAsync(new UfwDeleteRuleCommand(displayNumber), "deleting a rule during reordering", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RuleListResponse? canceledSnapshot = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
            RuleRecoveryResult recovery = await recoveryCoordinator.EnsurePresentAsync(journalEntry, canceledSnapshot, CancellationToken.None);
            if (!recovery.PresenceConfirmed)
            {
                throw new InvalidOperationException("The UFW delete process was canceled without a result and the active rule could not be recovered safely.");
            }

            throw;
        }

        RuleListResponse? afterDelete = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        if (delete.CancellationRequested || cancellationToken.IsCancellationRequested)
        {
            RuleRecoveryResult recovery = await recoveryCoordinator.EnsurePresentAsync(journalEntry, afterDelete, CancellationToken.None);
            if (!recovery.PresenceConfirmed)
            {
                throw new InvalidOperationException("The reorder request was canceled and the active rule could not be recovered safely.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException("The UFW delete process was canceled after it started.", cancellationToken);
        }

        List<int> afterDeleteOrder = [.. preMoveOrder];
        afterDeleteOrder.RemoveAt(currentIndex);
        bool deleteStateExpected = afterDelete is not null && FirewallRuleSnapshotMatcher.MatchesOrder(afterDelete, baseline, afterDeleteOrder);
        if (!deleteStateExpected)
        {
            RuleRecoveryResult recovery = await recoveryCoordinator.EnsurePresentAsync(journalEntry, afterDelete, CancellationToken.None);
            return RecoveryInterruption(move, recovery, delete.Diagnostic, "Firewall state diverged after deleting the rule.");
        }

        IUfwCommand insertCommand = CreatePlannedInsertionCommand(move, afterDelete!, afterDeleteOrder, specification);
        UfwProcessExecutionResult insert = await processExecutor.ExecuteAsync(insertCommand, "reinserting a moved rule", CancellationToken.None);
        RuleListResponse? afterInsert = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        List<int> expectedOrder = ApplyMove(preMoveOrder, move);
        if (afterInsert is not null && FirewallRuleSnapshotMatcher.MatchesOrder(afterInsert, baseline, expectedOrder))
        {
            await recoveryJournal.ClearAsync(CancellationToken.None);
            bool processFailure = !delete.Succeeded || !insert.Succeeded;
            string? diagnostic = UfwProcessDiagnostics.Combine(delete.Diagnostic, insert.Diagnostic);
            return new RuleReorderMoveExecutionResult.Completed(
                afterInsert,
                expectedOrder,
                new RuleReorderOperationReport(move, processFailure ? RuleReorderOperationStatus.AppliedAfterProcessFailure : RuleReorderOperationStatus.Applied, diagnostic));
        }

        RuleRecoveryResult postInsertRecovery = await recoveryCoordinator.EnsurePresentAsync(journalEntry, afterInsert, CancellationToken.None);
        return RecoveryInterruption(move, postInsertRecovery, UfwProcessDiagnostics.Combine(delete.Diagnostic, insert.Diagnostic), "Firewall state diverged after reinserting the moved rule.");
    }

    private RuleReorderMoveExecutionResult.Interrupted RecoveryInterruption(
        RuleReorderMove move,
        RuleRecoveryResult recovery,
        string? processDiagnostic,
        string divergenceDiagnostic)
    {
        string diagnostic = UfwProcessDiagnostics.Combine(processDiagnostic, divergenceDiagnostic, recovery.Diagnostic) ?? divergenceDiagnostic;
        if (!recovery.PresenceConfirmed)
        {
            _logger.LogError($"Rule reorder recovery failed for occurrence {move.OccurrenceId}: {diagnostic}");
            return Interrupted(
                recovery.Snapshot,
                new RuleReorderOperationReport(move, RuleReorderOperationStatus.RecoveryFailed, diagnostic),
                diagnostic,
                recoveryFailed: true);
        }

        RuleReorderOperationStatus status = recovery.InsertionAttempted
            ? RuleReorderOperationStatus.FailedAndRestored
            : RuleReorderOperationStatus.PresenceConfirmedAfterInterruption;
        _logger.LogWarning($"Rule reorder stopped after occurrence {move.OccurrenceId} was made safe: {diagnostic}");
        return Interrupted(recovery.Snapshot, new RuleReorderOperationReport(move, status, diagnostic), diagnostic);
    }

    private IUfwCommand CreatePlannedInsertionCommand(RuleReorderMove move, RuleListResponse afterDelete, IReadOnlyList<int> afterDeleteOrder, FirewallRuleSpecification specification)
    {
        int desiredOccurrenceIndex = move.BeforeOccurrenceId is int beforeOccurrenceId
            ? afterDeleteOrder.IndexOf(beforeOccurrenceId)
            : afterDelete.Rules.Count;
        if (desiredOccurrenceIndex < 0)
        {
            throw new InvalidOperationException("Reorder insertion anchor is missing from the expected state.");
        }
        UfwInsertionPlacement placement = UfwInsertionPlacementResolver.Resolve(afterDelete.Rules, specification.AddressFamily, desiredOccurrenceIndex);
        return placement.CreateCommand(specification, renderer);
    }

    private static ReorderRecoveryJournalEntry CreateJournalEntry(
        FirewallRuleSpecification specification,
        int originalFamilyPosition,
        RuleListResponse baseline,
        IReadOnlyList<int> preMoveOrder,
        int currentIndex)
    {
        int expectedMultiplicity = FirewallRuleSnapshotMatcher.CountMatches(baseline.Rules, specification);
        RuleRecoveryAnchor? previous = currentIndex > 0
            ? CreateAnchor(baseline.Rules[preMoveOrder[currentIndex - 1]])
            : null;
        RuleRecoveryAnchor? next = currentIndex + 1 < preMoveOrder.Count
            ? CreateAnchor(baseline.Rules[preMoveOrder[currentIndex + 1]])
            : null;
        return new ReorderRecoveryJournalEntry(ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION, specification, originalFamilyPosition, expectedMultiplicity, previous, next);
    }

    private static List<int> ApplyMove(IReadOnlyList<int> order, RuleReorderMove move)
    {
        List<int> result = [.. order];
        result.Remove(move.OccurrenceId);
        if (move.BeforeOccurrenceId is int beforeOccurrenceId)
        {
            int beforeIndex = result.IndexOf(beforeOccurrenceId);
            if (beforeIndex < 0)
            {
                throw new InvalidOperationException("Reorder insertion anchor is missing from the expected state.");
            }

            result.Insert(beforeIndex, move.OccurrenceId);
        }
        else
        {
            result.Add(move.OccurrenceId);
        }

        return result;
    }

    private static RuleRecoveryAnchor CreateAnchor(ListedFirewallRule rule) =>
        rule.Rule is not null
            ? new RuleRecoveryAnchor(rule.Rule, null)
            : new RuleRecoveryAnchor(null, rule.RawLine);

    private static RuleReorderMoveExecutionResult.Interrupted Interrupted(
        RuleListResponse? snapshot,
        RuleReorderOperationReport? operationReport,
        string? diagnostic,
        bool recoveryFailed = false) =>
        new(snapshot, operationReport, diagnostic, recoveryFailed);
}
