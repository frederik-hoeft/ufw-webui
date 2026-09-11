using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;

namespace Ufw.Client.Api;

internal sealed class IntentContextApiClient(HttpClient httpClient) : IIntentContextApiClient
{
    private static readonly Uri s_intentContextUri = new("api/v1/intent/context", UriKind.Relative);

    public async Task<IntentContextResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_intentContextUri, cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.IntentContextResponse, cancellationToken);
    }
}
