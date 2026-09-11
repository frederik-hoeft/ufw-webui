using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Api;

internal interface IIntentContextApiClient
{
    Task<IntentContextResponse> GetAsync(CancellationToken cancellationToken = default);
}
