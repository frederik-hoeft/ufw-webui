using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Intent;

/// <summary>
/// Provides signed-intent context obtained from the daemon while keeping IPC route details private to the gateway.
/// </summary>
public interface IIntentDaemonGateway
{
    Task<DaemonResult<IntentContextResponse>> GetContextAsync(CancellationToken cancellationToken = default);
}
