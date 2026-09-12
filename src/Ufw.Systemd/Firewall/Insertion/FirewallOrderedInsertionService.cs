using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall.Insertion;

internal sealed class FirewallOrderedInsertionService(
    IIntentVerifier intentVerifier,
    INonceStore nonceStore,
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard,
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
        return await executionGate.RunAsync(ct => ExecuteAsync(accepted, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteAsync(
        IntentVerificationResult.AcceptedInsertion accepted,
        CancellationToken cancellationToken)
    {
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

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
