using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallReorderExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IRuleReorderPlanner planner,
    IRuleReinsertabilityClassifier reinsertabilityClassifier,
    IRuleReorderRecoveryCoordinator recoveryCoordinator,
    IReorderRecoveryJournal recoveryJournal,
    IUfwRunner ufwRunner,
    IUfwRuleCommandRenderer renderer,
    ILogger logger) : IFirewallReorderExecutor
{
    private readonly ILogger<FirewallReorderExecutor> _logger = logger.Scoped<FirewallReorderExecutor>();

    public async Task<RuleReorderExecutionResult> ExecuteAsync(
        RuleReorderExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BaselineFingerprint);
        ArgumentNullException.ThrowIfNull(request.DesiredOrder);

        RuleListResponse? baseline = await TryReadSnapshotAsync(cancellationToken);
        if (baseline is null)
        {
            return Result(
                RuleReorderExecutionOutcome.StateUncertain,
                null,
                diagnostic: "The current authoritative firewall state could not be read before reordering.");
        }

        string actualFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        if (!string.Equals(actualFingerprint, request.BaselineFingerprint, StringComparison.Ordinal))
        {
            return Result(
                RuleReorderExecutionOutcome.StaleBaseline,
                baseline,
                diagnostic: "The authoritative firewall state no longer matches the signed reorder baseline.");
        }

        PreflightResult preflight;
        try
        {
            preflight = Preflight(baseline, request.DesiredOrder);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result(
                RuleReorderExecutionOutcome.PreconditionFailed,
                baseline,
                diagnostic: exception.Message);
        }

        if (preflight.Plan.Moves.Count == 0)
        {
            return Result(RuleReorderExecutionOutcome.Completed, baseline);
        }

        List<RuleReorderOperationReport> operationReports = [];
        List<int> currentOrder = Enumerable.Range(0, baseline.Rules.Count).ToList();
        RuleListResponse currentSnapshot = baseline;

        for (int moveIndex = 0; moveIndex < preflight.Plan.Moves.Count; moveIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RuleReorderMove move = preflight.Plan.Moves[moveIndex];
            MoveExecutionResult moveResult = await ExecuteMoveAsync(
                move,
                baseline,
                currentOrder,
                preflight.Classifications[move.OccurrenceId],
                cancellationToken);

            if (moveResult.OperationReport is not null)
            {
                operationReports.Add(moveResult.OperationReport);
            }

            if (moveResult.Completed)
            {
                currentOrder = moveResult.ResultingOrder!;
                currentSnapshot = moveResult.Snapshot!;
                continue;
            }

            IReadOnlyList<RuleReorderMove> blocked = preflight.Plan.Moves.Skip(moveIndex).ToArray();
            IReadOnlyList<RuleReorderMove> pending = CreateSafePendingPlan(
                baseline,
                moveResult.Snapshot,
                request.DesiredOrder,
                preflight.ImmutableOccurrences,
                preflight.KeepPriorities);
            RuleReorderExecutionOutcome outcome = moveResult.RecoveryFailed
                ? RuleReorderExecutionOutcome.RecoveryFailed
                : moveResult.Snapshot is null
                    ? RuleReorderExecutionOutcome.StateUncertain
                    : operationReports.Count == 0 && moveResult.OperationReport is null
                        ? RuleReorderExecutionOutcome.StaleBaseline
                        : RuleReorderExecutionOutcome.PartiallyCompleted;

            return new RuleReorderExecutionResult(
                outcome,
                moveResult.Snapshot,
                operationReports,
                blocked,
                pending,
                moveResult.Diagnostic);
        }

        return new RuleReorderExecutionResult(
            RuleReorderExecutionOutcome.Completed,
            currentSnapshot,
            operationReports,
            [],
            [],
            null);
    }

    private PreflightResult Preflight(RuleListResponse baseline, IReadOnlyList<int> desiredOrder)
    {
        int[] currentOrder = Enumerable.Range(0, baseline.Rules.Count).ToArray();
        Dictionary<int, RuleReinsertability> classifications = [];
        HashSet<int> immutableOccurrences = [];
        Dictionary<int, int> keepPriorities = [];

        for (int occurrenceId = 0; occurrenceId < baseline.Rules.Count; occurrenceId++)
        {
            RuleReinsertability classification = reinsertabilityClassifier.Classify(baseline.Rules[occurrenceId]);
            classifications.Add(occurrenceId, classification);
            if (!classification.IsReinsertable)
            {
                immutableOccurrences.Add(occurrenceId);
            }
            else
            {
                keepPriorities.Add(occurrenceId, classification.KeepPriority);
            }
        }

        ValidateAddressFamilyOrder(baseline.Rules, desiredOrder);
        RuleReorderPlan plan = planner.Plan(currentOrder, desiredOrder, immutableOccurrences, keepPriorities);
        foreach (RuleReorderMove move in plan.Moves)
        {
            RuleReinsertability classification = classifications[move.OccurrenceId];
            if (!classification.IsReinsertable || classification.Specification is null || classification.RenderedRule is null)
            {
                throw new InvalidOperationException(
                    classification.Reason ?? "The requested ordering would require moving a rule that cannot be reinserted safely.");
            }
        }

        return new PreflightResult(plan, classifications, immutableOccurrences, keepPriorities);
    }

    private async Task<MoveExecutionResult> ExecuteMoveAsync(
        RuleReorderMove move,
        RuleListResponse baseline,
        IReadOnlyList<int> preMoveOrder,
        RuleReinsertability classification,
        CancellationToken cancellationToken)
    {
        RuleListResponse? preMoveSnapshot = await TryReadSnapshotAsync(cancellationToken);
        if (preMoveSnapshot is null)
        {
            return MoveExecutionResult.Interrupted(
                null,
                null,
                "The authoritative firewall state could not be read before the next move started.");
        }
        if (!SnapshotMatchesOrder(preMoveSnapshot, baseline, preMoveOrder))
        {
            return MoveExecutionResult.Interrupted(
                preMoveSnapshot,
                null,
                "Authoritative state diverged before the next move started.");
        }

        int currentIndex = IndexOf(preMoveOrder, move.OccurrenceId);
        ListedFirewallRule listedRule = preMoveSnapshot.Rules[currentIndex];
        if (listedRule.DisplayNumber is not int displayNumber || classification.Specification is null)
        {
            return MoveExecutionResult.Interrupted(
                preMoveSnapshot,
                null,
                "The move target no longer has a usable UFW rule number.");
        }

        int originalFamilyPosition = UfwRulePositionResolver.GetFamilyPosition(preMoveSnapshot.Rules, currentIndex);
        ReorderRecoveryJournalEntry journalEntry = CreateJournalEntry(
            classification.Specification,
            originalFamilyPosition,
            baseline,
            preMoveOrder,
            currentIndex);
        await recoveryJournal.WriteAsync(journalEntry, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            await recoveryJournal.ClearAsync(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
        }

        ProcessExecution delete;
        try
        {
            delete = await ExecuteProcessAsync(new UfwDeleteRuleCommand(displayNumber), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RuleListResponse? canceledSnapshot = await TryReadSnapshotAsync(CancellationToken.None);
            RuleRecoveryResult recovery = await recoveryCoordinator.EnsurePresentAsync(journalEntry, canceledSnapshot, CancellationToken.None);
            if (!recovery.PresenceConfirmed)
            {
                throw new InvalidOperationException(
                    "The UFW delete process was canceled without a result and the active rule could not be recovered safely.");
            }
            throw;
        }
        RuleListResponse? afterDelete = await TryReadSnapshotAsync(CancellationToken.None);

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
        bool deleteStateExpected = afterDelete is not null && SnapshotMatchesOrder(afterDelete, baseline, afterDeleteOrder);
        if (!deleteStateExpected)
        {
            RuleRecoveryResult recovery = await recoveryCoordinator.EnsurePresentAsync(journalEntry, afterDelete, CancellationToken.None);
            return RecoveryInterruption(move, recovery, delete.Diagnostic, "Firewall state diverged after deleting the rule.");
        }

        IUfwCommand insertCommand = CreatePlannedInsertionCommand(move, afterDelete!, afterDeleteOrder, classification.Specification);
        ProcessExecution insert = await ExecuteProcessAsync(insertCommand, CancellationToken.None);
        RuleListResponse? afterInsert = await TryReadSnapshotAsync(CancellationToken.None);
        List<int> expectedOrder = ApplyMove(preMoveOrder, move);
        if (afterInsert is not null && SnapshotMatchesOrder(afterInsert, baseline, expectedOrder))
        {
            await recoveryJournal.ClearAsync(CancellationToken.None);
            bool processFailure = !delete.Succeeded || !insert.Succeeded;
            string? diagnostic = CombineDiagnostics(delete.Diagnostic, insert.Diagnostic);
            return MoveExecutionResult.Success(
                afterInsert,
                expectedOrder,
                new RuleReorderOperationReport(
                    move,
                    processFailure ? RuleReorderOperationStatus.AppliedAfterProcessFailure : RuleReorderOperationStatus.Applied,
                    diagnostic));
        }

        RuleRecoveryResult postInsertRecovery = await recoveryCoordinator.EnsurePresentAsync(journalEntry, afterInsert, CancellationToken.None);
        return RecoveryInterruption(
            move,
            postInsertRecovery,
            CombineDiagnostics(delete.Diagnostic, insert.Diagnostic),
            "Firewall state diverged after reinserting the moved rule.");
    }

    private MoveExecutionResult RecoveryInterruption(
        RuleReorderMove move,
        RuleRecoveryResult recovery,
        string? processDiagnostic,
        string divergenceDiagnostic)
    {
        string diagnostic = CombineDiagnostics(processDiagnostic, divergenceDiagnostic, recovery.Diagnostic)
            ?? divergenceDiagnostic;
        if (!recovery.PresenceConfirmed)
        {
            _logger.LogError($"Rule reorder recovery failed for occurrence {move.OccurrenceId}: {diagnostic}");
            return MoveExecutionResult.Interrupted(
                recovery.Snapshot,
                new RuleReorderOperationReport(move, RuleReorderOperationStatus.RecoveryFailed, diagnostic),
                diagnostic,
                recoveryFailed: true);
        }

        RuleReorderOperationStatus status = recovery.InsertionAttempted
            ? RuleReorderOperationStatus.FailedAndRestored
            : RuleReorderOperationStatus.PresenceConfirmedAfterInterruption;
        _logger.LogWarning($"Rule reorder stopped after occurrence {move.OccurrenceId} was made safe: {diagnostic}");
        return MoveExecutionResult.Interrupted(
            recovery.Snapshot,
            new RuleReorderOperationReport(move, status, diagnostic),
            diagnostic);
    }

    private IUfwCommand CreatePlannedInsertionCommand(
        RuleReorderMove move,
        RuleListResponse afterDelete,
        IReadOnlyList<int> afterDeleteOrder,
        FirewallRuleSpecification specification)
    {
        if (move.BeforeOccurrenceId is int beforeOccurrenceId)
        {
            int beforeIndex = IndexOf(afterDeleteOrder, beforeOccurrenceId);
            int familyPosition = UfwRulePositionResolver.GetFamilyPosition(afterDelete.Rules, beforeIndex);
            return new UfwInsertRuleCommand(familyPosition, specification, renderer);
        }

        return new UfwAddRuleCommand(specification, renderer);
    }

    private ReorderRecoveryJournalEntry CreateJournalEntry(
        FirewallRuleSpecification specification,
        int originalFamilyPosition,
        RuleListResponse baseline,
        IReadOnlyList<int> preMoveOrder,
        int currentIndex)
    {
        int expectedMultiplicity = RuleReorderRecoveryCoordinator.CountMatches(baseline.Rules, specification);
        RuleRecoveryAnchor? previous = currentIndex > 0
            ? CreateAnchor(baseline.Rules[preMoveOrder[currentIndex - 1]])
            : null;
        RuleRecoveryAnchor? next = currentIndex + 1 < preMoveOrder.Count
            ? CreateAnchor(baseline.Rules[preMoveOrder[currentIndex + 1]])
            : null;
        return new ReorderRecoveryJournalEntry(
            ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION,
            specification,
            originalFamilyPosition,
            expectedMultiplicity,
            previous,
            next);
    }

    private IReadOnlyList<RuleReorderMove> CreateSafePendingPlan(
        RuleListResponse baseline,
        RuleListResponse? finalSnapshot,
        IReadOnlyList<int> desiredOrder,
        IReadOnlySet<int> immutableOccurrences,
        IReadOnlyDictionary<int, int> keepPriorities)
    {
        if (finalSnapshot is null || !TryMapOccurrences(baseline, finalSnapshot, out int[]? currentOrder))
        {
            return [];
        }

        try
        {
            return planner.Plan(currentOrder!, desiredOrder, immutableOccurrences, keepPriorities).Moves;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return [];
        }
    }

    private static bool TryMapOccurrences(RuleListResponse baseline, RuleListResponse current, out int[]? order)
    {
        if (baseline.Rules.Count != current.Rules.Count || baseline.Active != current.Active)
        {
            order = null;
            return false;
        }

        bool[] used = new bool[baseline.Rules.Count];
        int[] mapped = new int[current.Rules.Count];
        for (int currentIndex = 0; currentIndex < current.Rules.Count; currentIndex++)
        {
            int match = -1;
            for (int baselineIndex = 0; baselineIndex < baseline.Rules.Count; baselineIndex++)
            {
                if (used[baselineIndex] || !FirewallRuleSemanticComparer.Equals(current.Rules[currentIndex], baseline.Rules[baselineIndex]))
                {
                    continue;
                }
                if (match >= 0)
                {
                    order = null;
                    return false;
                }
                match = baselineIndex;
            }
            if (match < 0)
            {
                order = null;
                return false;
            }
            used[match] = true;
            mapped[currentIndex] = match;
        }

        order = mapped;
        return true;
    }

    private async Task<RuleListResponse?> TryReadSnapshotAsync(CancellationToken cancellationToken)
    {
        FirewallRuleSnapshotReadResult read = await snapshotReader.ReadAsync(cancellationToken);
        return read.Error is null ? FirewallRuleSet.ToListResponse(read.Snapshot!) : null;
    }

    private async Task<ProcessExecution> ExecuteProcessAsync(IUfwCommand command, CancellationToken cancellationToken)
    {
        try
        {
            UfwProcessResult result = await ufwRunner.ExecuteAsync(command, cancellationToken);
            string? diagnostic = result.Succeeded && !result.CancellationRequested
                ? null
                : FormatProcessDiagnostic(result);
            return new ProcessExecution(result.Succeeded, result.CancellationRequested, diagnostic);
        }
        catch (ChildProcessException exception)
        {
            _logger.LogError(exception, "Failed to start UFW while executing a reorder move.");
            return new ProcessExecution(false, false, exception.Message);
        }
    }

    private static bool SnapshotMatchesOrder(
        RuleListResponse snapshot,
        RuleListResponse baseline,
        IReadOnlyList<int> expectedOrder)
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

    private static int IndexOf(IReadOnlyList<int> order, int occurrenceId)
    {
        for (int index = 0; index < order.Count; index++)
        {
            if (order[index] == occurrenceId)
            {
                return index;
            }
        }
        throw new InvalidOperationException($"Occurrence {occurrenceId} is missing from the expected ordering state.");
    }

    private static RuleRecoveryAnchor CreateAnchor(ListedFirewallRule rule) =>
        rule.Rule is not null
            ? new RuleRecoveryAnchor(rule.Rule, null)
            : new RuleRecoveryAnchor(null, rule.RawLine);

    private static void ValidateAddressFamilyOrder(IReadOnlyList<ListedFirewallRule> baselineRules, IReadOnlyList<int> desiredOrder)
    {
        bool ipv6Seen = false;
        foreach (int occurrenceId in desiredOrder)
        {
            if (occurrenceId < 0 || occurrenceId >= baselineRules.Count)
            {
                continue;
            }

            FirewallAddressFamily? family = baselineRules[occurrenceId].Rule?.AddressFamily;
            if (family == FirewallAddressFamily.IPv6)
            {
                ipv6Seen = true;
            }
            else if (family == FirewallAddressFamily.IPv4 && ipv6Seen)
            {
                throw new InvalidOperationException("UFW requires IPv4 rule materializations to remain before IPv6 rule materializations.");
            }
        }
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

    private static RuleReorderExecutionResult Result(
        RuleReorderExecutionOutcome outcome,
        RuleListResponse? finalSnapshot,
        string? diagnostic = null) =>
        new(outcome, finalSnapshot, [], [], [], diagnostic);

    private sealed record PreflightResult(
        RuleReorderPlan Plan,
        IReadOnlyDictionary<int, RuleReinsertability> Classifications,
        IReadOnlySet<int> ImmutableOccurrences,
        IReadOnlyDictionary<int, int> KeepPriorities);

    private sealed record ProcessExecution(bool Succeeded, bool CancellationRequested, string? Diagnostic);

    private sealed record MoveExecutionResult(
        bool Completed,
        bool RecoveryFailed,
        RuleListResponse? Snapshot,
        List<int>? ResultingOrder,
        RuleReorderOperationReport? OperationReport,
        string? Diagnostic)
    {
        public static MoveExecutionResult Success(
            RuleListResponse snapshot,
            List<int> resultingOrder,
            RuleReorderOperationReport report) =>
            new(true, false, snapshot, resultingOrder, report, report.Diagnostic);

        public static MoveExecutionResult Interrupted(
            RuleListResponse? snapshot,
            RuleReorderOperationReport? report,
            string? diagnostic,
            bool recoveryFailed = false) =>
            new(false, recoveryFailed, snapshot, null, report, diagnostic);
    }
}
