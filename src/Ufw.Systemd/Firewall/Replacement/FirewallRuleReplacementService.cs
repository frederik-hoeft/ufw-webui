using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall.Replacement;

internal sealed class FirewallRuleReplacementService(
    IIntentVerifier intentVerifier,
    ISignedMutationOrchestrator mutationOrchestrator,
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
        return await mutationOrchestrator.ExecuteAsync(accepted, ct => ExecuteOperationAsync(accepted, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteOperationAsync(IntentVerificationResult.AcceptedReplacement accepted, CancellationToken cancellationToken)
    {
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
