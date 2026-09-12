using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Security.Intent;
using ResponseOperationOutcome = Ufw.Shared.Ipc.Model.Responses.Domain.RuleReorderOperationOutcome;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallReorderService(
    IIntentVerifier intentVerifier,
    INonceStore nonceStore,
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard,
    IFirewallReorderExecutor executor) : IFirewallReorderService
{
    public async ValueTask<IResponsePayload> ReorderAsync(ReorderRulesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntentVerificationResult verification = intentVerifier.VerifyReorder(request);
        if (verification is IntentVerificationResult.Rejected rejected)
        {
            return rejected.Response;
        }

        IntentVerificationResult.AcceptedReorder accepted = (IntentVerificationResult.AcceptedReorder)verification;
        return await executionGate.RunAsync(ct => ExecuteAsync(accepted, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteAsync(
        IntentVerificationResult.AcceptedReorder accepted,
        CancellationToken cancellationToken)
    {
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        RuleReorderExecutionRequest executionRequest = new(
            accepted.Payload.BaselineFingerprint,
            accepted.Payload.DesiredOrder);
        RuleReorderExecutionResult result = await executor.ExecuteAsync(executionRequest, cancellationToken);
        return ToResponse(result);
    }

    private static RuleReorderResponse ToResponse(RuleReorderExecutionResult result) => new(
        MapOutcome(result.Outcome),
        result.FinalSnapshot,
        result.Operations.Select(MapOperation).ToArray(),
        result.BlockedOperations.Select(MapMove).ToArray(),
        result.PendingOperations.Select(MapMove).ToArray(),
        result.Diagnostic);

    private static RuleReorderOperationResponse MapOperation(RuleReorderOperationReport report) => new(
        MapMove(report.Move),
        report.Status switch
        {
            RuleReorderOperationStatus.Applied => ResponseOperationOutcome.Applied,
            RuleReorderOperationStatus.AppliedAfterProcessFailure => ResponseOperationOutcome.AppliedAfterProcessFailure,
            RuleReorderOperationStatus.FailedAndRestored => ResponseOperationOutcome.FailedAndRestored,
            RuleReorderOperationStatus.PresenceConfirmedAfterInterruption => ResponseOperationOutcome.PresenceConfirmedAfterInterruption,
            RuleReorderOperationStatus.RecoveryFailed => ResponseOperationOutcome.RecoveryFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(report), report.Status, "Unknown reorder operation status."),
        },
        report.Diagnostic);

    private static RuleReorderMoveResponse MapMove(RuleReorderMove move) => new(
        move.OccurrenceId,
        move.TargetIndex,
        move.BeforeOccurrenceId);

    private static RuleReorderOutcome MapOutcome(RuleReorderExecutionOutcome outcome) => outcome switch
    {
        RuleReorderExecutionOutcome.Completed => RuleReorderOutcome.Completed,
        RuleReorderExecutionOutcome.StaleBaseline => RuleReorderOutcome.StaleBaseline,
        RuleReorderExecutionOutcome.PreconditionFailed => RuleReorderOutcome.PreconditionFailed,
        RuleReorderExecutionOutcome.PartiallyCompleted => RuleReorderOutcome.PartiallyCompleted,
        RuleReorderExecutionOutcome.RecoveryFailed => RuleReorderOutcome.RecoveryFailed,
        RuleReorderExecutionOutcome.StateUncertain => RuleReorderOutcome.StateUncertain,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown reorder execution outcome."),
    };
}
