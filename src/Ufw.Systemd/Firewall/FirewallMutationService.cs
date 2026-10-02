using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallMutationService(
    IIntentVerifier intentVerifier,
    ISignedMutationOrchestrator mutationOrchestrator,
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
        return await mutationOrchestrator.ExecuteAsync(accepted, ct => mutationExecutor.AddAsync(accepted, ct), cancellationToken);
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
        return await mutationOrchestrator.ExecuteAsync(accepted, ct => mutationExecutor.DeleteAsync(accepted, ct), cancellationToken);
    }
}
