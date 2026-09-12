using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallMutationService(
    IIntentVerifier intentVerifier,
    INonceStore nonceStore,
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard,
    IFirewallMutationExecutor mutationExecutor) : IFirewallMutationService
{
    public async ValueTask<IResponsePayload> AddAsync(AddRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntentVerificationResult verification = intentVerifier.VerifyAdd(request);
        if (verification is IntentVerificationResult.Rejected rejected)
        {
            return rejected.Response;
        }

        IntentVerificationResult.AcceptedRuleMutation accepted = (IntentVerificationResult.AcceptedRuleMutation)verification;
        return await executionGate.RunAsync(ct => ExecuteAddAsync(accepted, ct), cancellationToken);
    }

    public async ValueTask<IResponsePayload> DeleteAsync(DeleteRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntentVerificationResult verification = intentVerifier.VerifyDelete(request);
        if (verification is IntentVerificationResult.Rejected rejected)
        {
            return rejected.Response;
        }

        IntentVerificationResult.AcceptedRuleMutation accepted = (IntentVerificationResult.AcceptedRuleMutation)verification;
        return await executionGate.RunAsync(ct => ExecuteDeleteAsync(accepted, ct), cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteAddAsync(IntentVerificationResult.AcceptedRuleMutation accepted, CancellationToken cancellationToken)
    {
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        return await mutationExecutor.AddAsync(accepted, cancellationToken);
    }

    private async Task<IResponsePayload> ExecuteDeleteAsync(IntentVerificationResult.AcceptedRuleMutation accepted, CancellationToken cancellationToken)
    {
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        if (!await nonceStore.TryConsumeAsync(accepted.Nonce, accepted.ExpiresAtUnix, cancellationToken))
        {
            return new ConflictResponse("Intent nonce has already been used.");
        }

        return await mutationExecutor.DeleteAsync(accepted, cancellationToken);
    }
}
