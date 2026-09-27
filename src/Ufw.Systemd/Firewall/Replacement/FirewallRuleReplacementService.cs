using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall.Replacement;

internal sealed class FirewallRuleReplacementService(
    IIntentVerifier intentVerifier,
    INonceStore nonceStore,
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard,
    IFirewallRuleReplacementExecutor executor) : IFirewallRuleReplacementService
{
    public async ValueTask<IResponsePayload> ReplaceAsync(ReplaceRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntentVerificationResult verification = intentVerifier.VerifyReplace(request);
        if (verification is IntentVerificationResult.Rejected rejected)
        {
            return rejected.Response;
        }

        IntentVerificationResult.AcceptedReplacement accepted = (IntentVerificationResult.AcceptedReplacement)verification;
        return await executionGate.RunAsync(ct => ExecuteAsync(accepted, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteAsync(IntentVerificationResult.AcceptedReplacement accepted, CancellationToken cancellationToken)
    {
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        RuleReplacementExecutionResult result = await executor.ExecuteAsync(accepted.Payload, cancellationToken);
        return new RuleReplacementResponse(
            result.Outcome switch
            {
                RuleReplacementExecutionOutcome.Completed => RuleReplacementOutcome.Completed,
                RuleReplacementExecutionOutcome.StaleBaseline => RuleReplacementOutcome.StaleBaseline,
                RuleReplacementExecutionOutcome.PreconditionFailed => RuleReplacementOutcome.PreconditionFailed,
                RuleReplacementExecutionOutcome.PartiallyCompleted => RuleReplacementOutcome.PartiallyCompleted,
                RuleReplacementExecutionOutcome.StateUncertain => RuleReplacementOutcome.StateUncertain,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown rule-replacement execution outcome."),
            },
            result.FinalSnapshot,
            result.ReplacementRule,
            result.RecoveryStatus switch
            {
                null => null,
                RuleReplacementRecoveryStatus.RestoredBaseline => RuleReplacementRecoveryOutcome.RestoredBaseline,
                RuleReplacementRecoveryStatus.Failed => RuleReplacementRecoveryOutcome.Failed,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.RecoveryStatus, "Unknown rule-replacement recovery status."),
            },
            result.Diagnostic);
    }
}
