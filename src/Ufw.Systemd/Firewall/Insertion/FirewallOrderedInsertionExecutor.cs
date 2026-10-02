using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall.Insertion;

internal sealed class FirewallOrderedInsertionExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IFirewallRuleInterfaceValidator interfaceValidator,
    IFirewallRuleCapabilityValidator capabilityValidator,
    IUfwProcessExecutor processExecutor,
    IUfwRuleCommandRenderer renderer,
    ILogger logger) : IFirewallOrderedInsertionExecutor
{
    private readonly ILogger<FirewallOrderedInsertionExecutor> _logger = logger.Scoped<FirewallOrderedInsertionExecutor>();

    public async Task<RuleInsertionExecutionResult> ExecuteAsync(InsertRulePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        RuleInsertionContract.ValidatePayload(payload);

        RuleListResponse? baseline = await snapshotReader.ReadAsync(cancellationToken).OrDefaultAsync();
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

        IResponsePayload? capabilityError = capabilityValidator.Validate(rule, baseline.Configuration);
        if (capabilityError is not null)
        {
            return Result(RuleInsertionExecutionOutcome.PreconditionFailed, baseline, GetResponseDiagnostic(capabilityError));
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
        UfwProcessExecutionResult process = await processExecutor.ExecuteAsync(command, "applying ordered rule insertion", cancellationToken);
        RuleListResponse? finalSnapshot = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        if (finalSnapshot is null)
        {
            return Result(RuleInsertionExecutionOutcome.StateUncertain, null, UfwProcessDiagnostics.Combine(process.Diagnostic, "The firewall state could not be read after ordered insertion."));
        }

        int expectedIndex = GetExpectedInsertionIndex(baseline.Rules, payload, anchor.Rule!.AddressFamily);
        if (FirewallRuleSnapshotMatcher.TryMatchSingleInsertion(baseline, finalSnapshot, rule, expectedIndex, out ListedFirewallRule? insertedRule))
        {
            _logger.LogInformation($"Inserted firewall rule '{identity}' at signed snapshot occurrence {payload.AnchorOccurrenceId} ({payload.Placement}).");
            return new RuleInsertionExecutionResult(RuleInsertionExecutionOutcome.Completed, finalSnapshot, insertedRule, process.Succeeded ? null : process.Diagnostic);
        }

        if (FirewallRuleSnapshotMatcher.Equivalent(baseline, finalSnapshot))
        {
            if (process.CancellationRequested && cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Result(RuleInsertionExecutionOutcome.PreconditionFailed, finalSnapshot, UfwProcessDiagnostics.Combine(process.Diagnostic, "The requested rule was not inserted."));
        }

        return Result(
            RuleInsertionExecutionOutcome.StateUncertain,
            finalSnapshot,
            UfwProcessDiagnostics.Combine(process.Diagnostic, "Firewall state diverged from the exact ordered-insertion postcondition."));
    }

    private IUfwCommand CreateCommand(RuleListResponse baseline, InsertRulePayload payload, ListedFirewallRule anchor, FirewallRuleSpecification rule)
    {
        FirewallAddressFamily family = anchor.Rule!.AddressFamily;
        if (payload.Placement == RuleInsertionPlacement.Before)
        {
            int position = UfwRulePositionResolver.GetUfwInsertPosition(baseline.Rules, payload.AnchorOccurrenceId);
            return new UfwInsertRuleCommand(position, rule, renderer);
        }

        int? nextFamilyOccurrence = UfwRulePositionResolver.FindNextFamilyOccurrence(baseline.Rules, payload.AnchorOccurrenceId, family);
        if (nextFamilyOccurrence.HasValue)
        {
            int position = UfwRulePositionResolver.GetUfwInsertPosition(baseline.Rules, nextFamilyOccurrence.Value);
            return new UfwInsertRuleCommand(position, rule, renderer);
        }

        return new UfwAddRuleCommand(rule, renderer);
    }

    private static int GetExpectedInsertionIndex(IReadOnlyList<ListedFirewallRule> baseline, InsertRulePayload payload, FirewallAddressFamily family)
    {
        if (payload.Placement == RuleInsertionPlacement.Before)
        {
            return payload.AnchorOccurrenceId;
        }

        int? nextFamilyOccurrence = UfwRulePositionResolver.FindNextFamilyOccurrence(baseline, payload.AnchorOccurrenceId, family);
        if (nextFamilyOccurrence.HasValue)
        {
            return nextFamilyOccurrence.Value;
        }

        if (family == FirewallAddressFamily.IPv4)
        {
            for (int index = 0; index < baseline.Count; index++)
            {
                if (ListedFirewallRuleFamily.GetObservedFamily(baseline[index]) == FirewallAddressFamily.IPv6)
                {
                    return index;
                }
            }
        }

        return baseline.Count;
    }

    private static string GetResponseDiagnostic(IResponsePayload response) => response switch
    {
        Ufw.Shared.Ipc.Model.Responses.ErrorResponse error => error.Message ?? "Ordered insertion precondition validation failed.",
        _ => "Ordered insertion precondition validation failed.",
    };

    private static RuleInsertionExecutionResult Result(RuleInsertionExecutionOutcome outcome, RuleListResponse? finalSnapshot, string? diagnostic) =>
        new(outcome, finalSnapshot, null, diagnostic);
}
