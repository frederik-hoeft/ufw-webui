using System.Net.Http.Json;
using Ufw.Client.Serialization;

namespace Ufw.Client.Api;

internal sealed class NetworkInterfaceApiClient(HttpClient httpClient) : INetworkInterfaceApiClient
{
    private static readonly Uri s_interfacesUri = new("api/v1/network-interfaces", UriKind.Relative);
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

        Uri uri = new($"api/v1/network-interfaces/{interfaceId:D}/comment", UriKind.Relative);
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

        Uri uri = new($"api/v1/network-interfaces/{interfaceId:D}/visibility", UriKind.Relative);
        UpdateNetworkInterfaceVisibilityRequest request = new() { IsVisible = isVisible };
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateNetworkInterfaceVisibilityRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse, cancellationToken);
    }
}
