using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

internal sealed class SignedMutationOrchestrator(
    INonceStore nonceStore,
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard) : ISignedMutationOrchestrator
{
    public Task<IResponsePayload> ExecuteAsync(
        IntentVerificationResult.Accepted accepted,
        Func<CancellationToken, Task<IResponsePayload>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(operation);
        return executionGate.RunAsync(ct => ExecuteWithinGateAsync(accepted, operation, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteWithinGateAsync(
        IntentVerificationResult.Accepted accepted,
        Func<CancellationToken, Task<IResponsePayload>> operation,
        CancellationToken cancellationToken)
    {
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        return await operation(cancellationToken);
    }
}
