using System.Net.Http.Json;
using Ufw.Client.Serialization;

namespace Ufw.Client.Api;

internal sealed class KnownHostApiClient(HttpClient httpClient) : IKnownHostApiClient
{
    private static readonly Uri s_hostsUri = new("api/v1/known-hosts", UriKind.Relative);

    public async Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_hostsUri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }

    public async Task<KnownHostInventoryResponse> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.CreateKnownHostRequest);
        using HttpResponseMessage response = await httpClient.PostAsync(s_hostsUri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }

    public async Task<KnownHostInventoryResponse> UpdateAsync(Guid hostId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        ValidateHostId(hostId);
        ArgumentNullException.ThrowIfNull(request);
        Uri uri = new($"api/v1/known-hosts/{hostId:D}", UriKind.Relative);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateKnownHostRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }

    public async Task<KnownHostInventoryResponse> DeleteAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        ValidateHostId(hostId);
        Uri uri = new($"api/v1/known-hosts/{hostId:D}", UriKind.Relative);
        using HttpResponseMessage response = await httpClient.DeleteAsync(uri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }

    private static void ValidateHostId(Guid hostId)
    {
        if (hostId == Guid.Empty)
        {
            throw new ArgumentException("Known host ID must not be empty.", nameof(hostId));
        }
    }
}
