using Ufw.Shared.Ipc.Model;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

/// <summary>
/// Serializes verified signed mutations and enforces recovery before durable nonce consumption and operation execution.
/// </summary>
internal interface ISignedMutationOrchestrator
{
    Task<IResponsePayload> ExecuteAsync(
        IntentVerificationResult.Accepted accepted,
        Func<CancellationToken, Task<IResponsePayload>> operation,
        CancellationToken cancellationToken);
}
