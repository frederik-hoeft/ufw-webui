using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Security.Intent;
using ResponseOperationOutcome = Ufw.Shared.Ipc.Model.Responses.Domain.RuleBatchDeleteOperationOutcome;

namespace Ufw.Systemd.Firewall.Deletion;

internal sealed class FirewallBatchDeleteService(
    IIntentVerifier intentVerifier,
    INonceStore nonceStore,
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard,
    IFirewallBatchDeleteExecutor executor) : IFirewallBatchDeleteService
{
    public async ValueTask<IResponsePayload> DeleteAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntentVerificationResult verification = intentVerifier.VerifyBatchDelete(request);
        if (verification is IntentVerificationResult.Rejected rejected)
        {
            return rejected.Response;
        }

        IntentVerificationResult.AcceptedBatchDelete accepted = (IntentVerificationResult.AcceptedBatchDelete)verification;
        return await executionGate.RunAsync(ct => ExecuteAsync(accepted, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteAsync(IntentVerificationResult.AcceptedBatchDelete accepted, CancellationToken cancellationToken)
    {
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        RuleBatchDeleteExecutionResult result = await executor.ExecuteAsync(accepted.Payload, cancellationToken);
        return ToResponse(result);
    }

    private static RuleBatchDeleteResponse ToResponse(RuleBatchDeleteExecutionResult result) => new(
        MapOutcome(result.Outcome),
        result.FinalSnapshot,
        result.Operations.Select(MapOperation).ToArray(),
        result.PendingOccurrenceIds,
        result.Diagnostic);

    private static RuleBatchDeleteOperationResponse MapOperation(RuleBatchDeleteOperationReport report) => new(
        report.OccurrenceId,
        report.RuleId,
        report.Status switch
        {
            RuleBatchDeleteOperationStatus.Deleted => ResponseOperationOutcome.Deleted,
            RuleBatchDeleteOperationStatus.DeletedAfterProcessFailure => ResponseOperationOutcome.DeletedAfterProcessFailure,
            RuleBatchDeleteOperationStatus.Failed => ResponseOperationOutcome.Failed,
            RuleBatchDeleteOperationStatus.StateUncertain => ResponseOperationOutcome.StateUncertain,
            _ => throw new ArgumentOutOfRangeException(nameof(report), report.Status, "Unknown batch-delete operation status."),
        },
        report.Diagnostic);

    private static RuleBatchDeleteOutcome MapOutcome(RuleBatchDeleteExecutionOutcome outcome) => outcome switch
    {
        RuleBatchDeleteExecutionOutcome.Completed => RuleBatchDeleteOutcome.Completed,
        RuleBatchDeleteExecutionOutcome.StaleBaseline => RuleBatchDeleteOutcome.StaleBaseline,
        RuleBatchDeleteExecutionOutcome.PreconditionFailed => RuleBatchDeleteOutcome.PreconditionFailed,
        RuleBatchDeleteExecutionOutcome.PartiallyCompleted => RuleBatchDeleteOutcome.PartiallyCompleted,
        RuleBatchDeleteExecutionOutcome.StateUncertain => RuleBatchDeleteOutcome.StateUncertain,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown batch-delete execution outcome."),
    };
}
