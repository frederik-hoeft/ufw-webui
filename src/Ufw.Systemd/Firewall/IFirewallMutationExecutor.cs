using Ufw.Shared.Ipc.Model;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

internal interface IFirewallMutationExecutor
{
    Task<IResponsePayload> AddAsync(IntentVerificationResult.Accepted intent, CancellationToken cancellationToken);

    Task<IResponsePayload> DeleteAsync(IntentVerificationResult.Accepted intent, CancellationToken cancellationToken);
}
