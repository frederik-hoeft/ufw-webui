using System.Net.Http.Json;
using Ufw.Shared.Web;
using Ufw.Web.Client.Infrastructure.Http;
using Ufw.Web.Client.Infrastructure.Serialization;

namespace Ufw.Web.Client.Features.KnownHosts.Api;

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
        ValidateHostId(hostId);
        ArgumentNullException.ThrowIfNull(request);
        Uri uri = BuildHostUri(hostId);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateKnownHostRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }

    public async Task<KnownHostInventoryResponse> DeleteAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        ValidateHostId(hostId);
        Uri uri = BuildHostUri(hostId);
        using HttpResponseMessage response = await httpClient.DeleteAsync(uri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.KnownHostInventoryResponse, cancellationToken);
    }

    private static Uri BuildHostUri(Guid hostId) => SimpleUriBuilder.Create(HOSTS_PATH)
        .AppendPath(hostId.ToString("D"))
        .BuildUri(UriKind.Relative);

    private static void ValidateHostId(Guid hostId)
    {
        if (hostId == Guid.Empty)
        {
            throw new ArgumentException("Known host ID must not be empty.", nameof(hostId));
        }
    }
}
