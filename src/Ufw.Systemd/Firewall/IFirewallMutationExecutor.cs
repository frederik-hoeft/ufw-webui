using Ufw.Shared.Ipc.Model;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

internal interface IFirewallMutationExecutor
{
    Task<IResponsePayload> AddAsync(IntentVerificationResult.AcceptedRuleMutation intent, FirewallSnapshot snapshot, CancellationToken cancellationToken);

    Task<IResponsePayload> DeleteAsync(IntentVerificationResult.AcceptedRuleMutation intent, FirewallSnapshot snapshot, CancellationToken cancellationToken);
}
