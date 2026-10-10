using System.Net.Http.Json;
using Ufw.Web.Client.Api;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Api.KnownHosts;

internal sealed class KnownHostApiClient(HttpClient httpClient) : IKnownHostApiClient
{
    private const string HOSTS_PATH = "api/v1/known-hosts";
    private static readonly Uri s_hostsUri = new(HOSTS_PATH, UriKind.Relative);

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
        Uri uri = ApiResourceUri.ForId(HOSTS_PATH, hostId, nameof(hostId), "Known host");
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateKnownHostRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }

    public async Task<KnownHostInventoryResponse> ReconcileDnsAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        Uri uri = ApiResourceUri.ForId(HOSTS_PATH, hostId, nameof(hostId), "Known host", "dns", "reconcile");
        using HttpResponseMessage response = await httpClient.PostAsync(uri, content: null, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }

    public async Task<KnownHostInventoryResponse> DeleteAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        Uri uri = ApiResourceUri.ForId(HOSTS_PATH, hostId, nameof(hostId), "Known host");
        using HttpResponseMessage response = await httpClient.DeleteAsync(uri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }
}
