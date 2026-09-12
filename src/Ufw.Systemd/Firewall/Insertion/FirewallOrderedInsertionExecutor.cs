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

namespace Ufw.Systemd.Firewall.Insertion;

internal sealed class FirewallOrderedInsertionExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IFirewallRuleInterfaceValidator interfaceValidator,
    IUfwRunner ufwRunner,
    IUfwRuleCommandRenderer renderer,
    ILogger logger) : IFirewallOrderedInsertionExecutor
{
    private readonly ILogger<FirewallOrderedInsertionExecutor> _logger = logger.Scoped<FirewallOrderedInsertionExecutor>();

    public async Task<RuleInsertionExecutionResult> ExecuteAsync(
        InsertRulePayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        RuleInsertionContract.ValidatePayload(payload);

        RuleListResponse? baseline = await TryReadSnapshotAsync(cancellationToken);
        if (baseline is null)
        {
            return Result(RuleInsertionExecutionOutcome.StateUncertain, null, "The authoritative firewall state could not be read before ordered insertion.");
        }
        if (!string.Equals(FirewallRuleSnapshotFingerprint.Compute(baseline), payload.BaselineFingerprint, StringComparison.Ordinal))
        {
            return Result(RuleInsertionExecutionOutcome.StaleBaseline, baseline, "The authoritative firewall state no longer matches the signed insertion baseline.");
        }

        ListedFirewallRule anchor;
        FirewallRuleSpecification rule = RuleSpecificationNormalizer.Normalize(payload.Rule);
        try
        {
            anchor = RuleInsertionContract.ResolveAnchor(baseline.Rules, payload);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result(RuleInsertionExecutionOutcome.PreconditionFailed, baseline, exception.Message);
        }

        IResponsePayload? interfaceError = interfaceValidator.Validate(rule);
        if (interfaceError is not null)
        {
            RuleInsertionExecutionOutcome outcome = interfaceError is ModelValidationErrorResponse
                ? RuleInsertionExecutionOutcome.PreconditionFailed
                : RuleInsertionExecutionOutcome.StateUncertain;
            return Result(outcome, baseline, GetResponseDiagnostic(interfaceError));
        }

        string identity = RuleIdentity.Compute(rule);
        if (baseline.Rules.Any(existing => string.Equals(existing.RuleId, identity, StringComparison.Ordinal)))
        {
            return Result(RuleInsertionExecutionOutcome.PreconditionFailed, baseline, "A semantically identical rule already exists.");
        }

        IUfwCommand command = CreateCommand(baseline, payload, anchor, rule);
        ProcessExecution process = await ExecuteProcessAsync(command, cancellationToken);
        RuleListResponse? finalSnapshot = await TryReadSnapshotAsync(CancellationToken.None);
        if (finalSnapshot is null)
        {
            return Result(
                RuleInsertionExecutionOutcome.StateUncertain,
                null,
                CombineDiagnostics(process.Diagnostic, "The firewall state could not be read after ordered insertion."));
        }

        int expectedIndex = GetExpectedInsertionIndex(baseline.Rules, payload, anchor.Rule!.AddressFamily);
        if (TryMatchExactPostcondition(baseline, finalSnapshot, rule, expectedIndex, out ListedFirewallRule? insertedRule))
        {
            _logger.LogInformation($"Inserted firewall rule '{identity}' at signed snapshot occurrence {payload.AnchorOccurrenceId} ({payload.Placement}).");
            return new RuleInsertionExecutionResult(
                RuleInsertionExecutionOutcome.Completed,
                finalSnapshot,
                insertedRule,
                process.Succeeded ? null : process.Diagnostic);
        }

        if (SnapshotsEqual(baseline, finalSnapshot))
        {
            if (process.CancellationRequested && cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Result(
                RuleInsertionExecutionOutcome.PreconditionFailed,
                finalSnapshot,
                CombineDiagnostics(process.Diagnostic, "The requested rule was not inserted."));
        }

        return Result(
            RuleInsertionExecutionOutcome.StateUncertain,
            finalSnapshot,
            CombineDiagnostics(process.Diagnostic, "Firewall state diverged from the exact ordered-insertion postcondition."));
    }

    private IUfwCommand CreateCommand(
        RuleListResponse baseline,
        InsertRulePayload payload,
        ListedFirewallRule anchor,
        FirewallRuleSpecification rule)
    {
        FirewallAddressFamily family = anchor.Rule!.AddressFamily;
        if (payload.Placement == RuleInsertionPlacement.Before)
        {
            int position = UfwRulePositionResolver.GetFamilyPosition(baseline.Rules, payload.AnchorOccurrenceId);
            return new UfwInsertRuleCommand(position, rule, renderer);
        }

        int? nextFamilyOccurrence = UfwRulePositionResolver.FindNextFamilyOccurrence(
            baseline.Rules,
            payload.AnchorOccurrenceId,
            family);
        if (nextFamilyOccurrence.HasValue)
        {
            int position = UfwRulePositionResolver.GetFamilyPosition(baseline.Rules, nextFamilyOccurrence.Value);
            return new UfwInsertRuleCommand(position, rule, renderer);
        }

        return new UfwAddRuleCommand(rule, renderer);
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
            _logger.LogError(exception, "UFW execution failed while applying ordered rule insertion. Authoritative state will be reconciled before classifying the result.");
            return new ProcessExecution(false, false, exception.Message);
        }
    }

    private async Task<RuleListResponse?> TryReadSnapshotAsync(CancellationToken cancellationToken)
    {
        FirewallRuleSnapshotReadResult read = await snapshotReader.ReadAsync(cancellationToken);
        return read.Error is null ? FirewallRuleSet.ToListResponse(read.Snapshot!) : null;
    }

    private static int GetExpectedInsertionIndex(
        IReadOnlyList<ListedFirewallRule> baseline,
        InsertRulePayload payload,
        FirewallAddressFamily family)
    {
        if (payload.Placement == RuleInsertionPlacement.Before)
        {
            return payload.AnchorOccurrenceId;
        }

        int? nextFamilyOccurrence = UfwRulePositionResolver.FindNextFamilyOccurrence(
            baseline,
            payload.AnchorOccurrenceId,
            family);
        if (nextFamilyOccurrence.HasValue)
        {
            return nextFamilyOccurrence.Value;
        }

        if (family == FirewallAddressFamily.IPv4)
        {
            for (int index = 0; index < baseline.Count; index++)
            {
                if (UfwRulePositionResolver.GetObservedFamily(baseline[index]) == FirewallAddressFamily.IPv6)
                {
                    return index;
                }
            }
        }

        return baseline.Count;
    }

    private static bool TryMatchExactPostcondition(
        RuleListResponse baseline,
        RuleListResponse current,
        FirewallRuleSpecification inserted,
        int insertionIndex,
        out ListedFirewallRule? insertedRule)
    {
        insertedRule = null;
        if (current.Active != baseline.Active || current.Rules.Count != baseline.Rules.Count + 1)
        {
            return false;
        }
        if (insertionIndex < 0 || insertionIndex >= current.Rules.Count)
        {
            return false;
        }

        for (int currentIndex = 0, baselineIndex = 0; currentIndex < current.Rules.Count; currentIndex++)
        {
            if (currentIndex == insertionIndex)
            {
                ListedFirewallRule candidate = current.Rules[currentIndex];
                if (candidate.Rule is null || !FirewallRuleSemanticComparer.Equals(candidate.Rule, inserted))
                {
                    return false;
                }
                insertedRule = candidate;
                continue;
            }

            if (baselineIndex >= baseline.Rules.Count
                || !FirewallRuleSemanticComparer.Equals(current.Rules[currentIndex], baseline.Rules[baselineIndex]))
            {
                return false;
            }
            baselineIndex++;
        }

        return insertedRule is not null;
    }

    private static bool SnapshotsEqual(RuleListResponse left, RuleListResponse right)
    {
        if (left.Active != right.Active || left.Rules.Count != right.Rules.Count)
        {
            return false;
        }
        for (int index = 0; index < left.Rules.Count; index++)
        {
            if (!FirewallRuleSemanticComparer.Equals(left.Rules[index], right.Rules[index]))
            {
                return false;
            }
        }
        return true;
    }

    private static string GetResponseDiagnostic(IResponsePayload response) => response switch
    {
        Ufw.Shared.Ipc.Model.Responses.ErrorResponse error => error.Message ?? "Ordered insertion precondition validation failed.",
        _ => "Ordered insertion precondition validation failed.",
    };

    private static string FormatProcessDiagnostic(UfwProcessResult result)
    {
        string details = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return result.CancellationRequested
            ? "UFW insertion was canceled after the process started."
            : $"UFW insertion exited with code {result.ExitCode}: {details}";
    }

    private static string? CombineDiagnostics(params string?[] diagnostics)
    {
        string[] nonEmpty = diagnostics
            .Where(static diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
            .Select(static diagnostic => diagnostic!)
            .ToArray();
        return nonEmpty.Length == 0 ? null : string.Join(' ', nonEmpty);
    }

    private static RuleInsertionExecutionResult Result(
        RuleInsertionExecutionOutcome outcome,
        RuleListResponse? finalSnapshot,
        string? diagnostic) =>
        new(outcome, finalSnapshot, null, diagnostic);

    private sealed record ProcessExecution(bool Succeeded, bool CancellationRequested, string? Diagnostic);
}
