using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Services.Intent;

/// <summary>
/// Provides signed-intent context obtained from the daemon while keeping IPC route details private to the gateway.
/// </summary>
public interface IIntentDaemonGateway
{
    Task<IntentContextResponse> GetContextAsync(CancellationToken cancellationToken = default);
}
