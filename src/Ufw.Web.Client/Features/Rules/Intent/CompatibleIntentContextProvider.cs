using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Intent;

namespace Ufw.Web.Client.Features.Rules.Intent;

internal sealed class CompatibleIntentContextProvider(IIntentContextApiClient apiClient) : ICompatibleIntentContextProvider
{
    public async Task<string> GetDeploymentIdAsync(CancellationToken cancellationToken = default)
    {
        IntentContextResponse context = await apiClient.GetAsync(cancellationToken);
        if (context.ProtocolVersion != IntentProtocol.VERSION)
        {
            throw new ApiProtocolException($"Intent protocol mismatch. Client supports version {IntentProtocol.VERSION}, server reports {context.ProtocolVersion}.");
        }

        return context.DeploymentId;
    }
}
