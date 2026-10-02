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

        FirewallRuleSpecification rule = RuleSpecificationNormalizer.Normalize(payload.Rule);
        if (!RuleInsertionContract.TryResolveAnchor(baseline.Rules, payload, out _, out string? anchorDiagnostic))
        {
            return Result(RuleInsertionExecutionOutcome.PreconditionFailed, baseline, anchorDiagnostic);
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

        int desiredOccurrenceIndex = payload.Placement == RuleInsertionPlacement.Before
            ? payload.AnchorOccurrenceId
            : payload.AnchorOccurrenceId + 1;
        UfwInsertionPlacement placement = UfwInsertionPlacementResolver.Resolve(baseline.Rules, rule.AddressFamily, desiredOccurrenceIndex);
        UfwProcessExecutionResult process = await processExecutor.ExecuteAsync(placement.CreateCommand(rule, renderer), "applying ordered rule insertion", cancellationToken);
        RuleListResponse? finalSnapshot = await snapshotReader.ReadAsync(CancellationToken.None).OrDefaultAsync();
        if (finalSnapshot is null)
        {
            return Result(RuleInsertionExecutionOutcome.StateUncertain, null, UfwProcessDiagnostics.Combine(process.Diagnostic, "The firewall state could not be read after ordered insertion."));
        }

        if (FirewallRuleSnapshotMatcher.TryMatchSingleInsertion(baseline, finalSnapshot, rule, placement.ExpectedOccurrenceIndex, out ListedFirewallRule? insertedRule))
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

    private static string GetResponseDiagnostic(IResponsePayload response) => response switch
    {
        Ufw.Shared.Ipc.Model.Responses.ErrorResponse error => error.Message ?? "Ordered insertion precondition validation failed.",
        _ => "Ordered insertion precondition validation failed.",
    };

    private static RuleInsertionExecutionResult Result(RuleInsertionExecutionOutcome outcome, RuleListResponse? finalSnapshot, string? diagnostic) =>
        new(outcome, finalSnapshot, null, diagnostic);
}
