using System.Net.Http.Json;
using Ufw.Shared.Web;
using Ufw.Web.Client.Api;
using Ufw.Web.Model.V1.NetworkInterfaces;

namespace Ufw.Web.Client.Api.NetworkInterfaces;

internal sealed class NetworkInterfaceApiClient(HttpClient httpClient) : INetworkInterfaceApiClient
{
    private const string INTERFACES_PATH = "api/v1/network-interfaces";
    private static readonly Uri s_interfacesUri = new(INTERFACES_PATH, UriKind.Relative);
    private static readonly Uri s_reconcileUri = new("api/v1/network-interfaces/reconcile", UriKind.Relative);

    public async Task<NetworkInterfaceInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_interfacesUri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse, cancellationToken);
    }

    public async Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(s_reconcileUri, content: null, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse, cancellationToken);
    }

    public async Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(Guid interfaceId, string? comment, CancellationToken cancellationToken = default)
    {
        if (interfaceId == Guid.Empty)
        {
            throw new ArgumentException("Interface ID must not be empty.", nameof(interfaceId));
        }

        Uri uri = BuildInterfaceUri(interfaceId, "comment");
        UpdateNetworkInterfaceCommentRequest request = new() { Comment = comment };
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateNetworkInterfaceCommentRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse, cancellationToken);
    }

    public async Task<NetworkInterfaceInventoryResponse> UpdateVisibilityAsync(Guid interfaceId, bool isVisible, CancellationToken cancellationToken = default)
    {
        if (interfaceId == Guid.Empty)
        {
            throw new ArgumentException("Interface ID must not be empty.", nameof(interfaceId));
        }

        Uri uri = BuildInterfaceUri(interfaceId, "visibility");
        UpdateNetworkInterfaceVisibilityRequest request = new() { IsVisible = isVisible };
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateNetworkInterfaceVisibilityRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse, cancellationToken);
    }

    private static Uri BuildInterfaceUri(Guid interfaceId, ReadOnlySpan<char> resource) => SimpleUriBuilder.Create(INTERFACES_PATH)
        .AppendPath(interfaceId.ToString("D"))
        .AppendPath(resource)
        .BuildUri(UriKind.Relative);
}
