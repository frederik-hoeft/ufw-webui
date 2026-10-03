using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Intent;

internal sealed class IntentDaemonGateway(IUfwClient ufwClient) : IIntentDaemonGateway
{
    private const string INTENT_CONTEXT_ROUTE = "/api/v1/intent/context";

    public Task<DaemonResult<IntentContextResponse>> GetContextAsync(CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync<IntentContextResponse>(RequestMethod.Get, INTENT_CONTEXT_ROUTE, cancellationToken));
}
