using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall.Insertion;

internal sealed class FirewallOrderedInsertionService(
    IIntentVerifier intentVerifier,
    ISignedMutationOrchestrator mutationOrchestrator,
    IFirewallOrderedInsertionExecutor executor) : IFirewallOrderedInsertionService
{
    public async ValueTask<IResponsePayload> InsertAsync(InsertRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntentVerificationResult verification = intentVerifier.VerifyInsert(request);
        if (verification is IntentVerificationResult.Rejected rejected)
        {
            return rejected.Response;
        }

        IntentVerificationResult.AcceptedInsertion accepted = (IntentVerificationResult.AcceptedInsertion)verification;
        return await mutationOrchestrator.ExecuteAsync(accepted, ct => ExecuteOperationAsync(accepted, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteOperationAsync(IntentVerificationResult.AcceptedInsertion accepted, CancellationToken cancellationToken)
    {
        RuleInsertionExecutionResult result = await executor.ExecuteAsync(accepted.Payload, cancellationToken);
        return new RuleInsertionResponse(
            result.Outcome switch
            {
                RuleInsertionExecutionOutcome.Completed => RuleInsertionOutcome.Completed,
                RuleInsertionExecutionOutcome.StaleBaseline => RuleInsertionOutcome.StaleBaseline,
                RuleInsertionExecutionOutcome.PreconditionFailed => RuleInsertionOutcome.PreconditionFailed,
                RuleInsertionExecutionOutcome.StateUncertain => RuleInsertionOutcome.StateUncertain,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown insertion execution outcome."),
            },
            result.FinalSnapshot,
            result.InsertedRule,
            result.Diagnostic);
    }
}
