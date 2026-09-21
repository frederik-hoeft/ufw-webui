using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Infrastructure.Intent;

internal interface IIntentContextApiClient
{
    Task<IntentContextResponse> GetAsync(CancellationToken cancellationToken = default);
}
