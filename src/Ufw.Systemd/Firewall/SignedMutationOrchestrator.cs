using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

internal sealed class SignedMutationOrchestrator(
    INonceStore nonceStore,
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard,
    IFirewallRuleSnapshotReader snapshotReader) : ISignedMutationOrchestrator
{
    public Task<IResponsePayload> ExecuteAsync(
        IntentVerificationResult.Accepted accepted,
        Func<FirewallSnapshot, CancellationToken, Task<IResponsePayload>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(operation);
        return executionGate.RunAsync(ct => ExecuteWithinGateAsync(accepted, operation, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteWithinGateAsync(
        IntentVerificationResult.Accepted accepted,
        Func<FirewallSnapshot, CancellationToken, Task<IResponsePayload>> operation,
        CancellationToken cancellationToken)
    {
        // Recovery can mutate the firewall; observe its state only after recovery completes.
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        FirewallRuleSnapshotReadResult read = await snapshotReader.ReadAsync(cancellationToken);
        if (!read.TryGetSnapshot(out FirewallSnapshot? snapshot, out IResponsePayload? readError))
        {
            return readError!;
        }

        // Reassess the freshly observed state inside the execution gate; never trust client-provided assessments.
        FirewallStateAssessment assessment = FirewallStateAssessmentEvaluator.Evaluate(snapshot.Rules);
        if (!assessment.IsClean)
        {
            return new UnprocessableContentResponse("The authoritative firewall has duplicate semantic rule identities. Repair the ambiguous state directly with UFW before making further changes.")
            {
                Code = FirewallStateErrorCodes.AMBIGUOUS_STATE,
            };
        }

        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        return await operation(snapshot, cancellationToken);
    }
}
