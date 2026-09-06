using System.Net.Http.Json;
using Ufw.Client.Serialization;

namespace Ufw.Client.Api;

internal sealed class NetworkInterfaceApiClient(HttpClient httpClient) : INetworkInterfaceApiClient
{
    private static readonly Uri s_interfacesUri = new("api/v1/network-interfaces", UriKind.Relative);
    private static readonly Uri s_reconcileUri = new("api/v1/network-interfaces/reconcile", UriKind.Relative);

    public bool UsesMockData => false;

    public async Task<NetworkInterfaceInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_interfacesUri, cancellationToken);
        return await response.ReadRequiredAsync(
            ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse,
            cancellationToken);
    }

    public async Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(s_reconcileUri, content: null, cancellationToken);
        return await response.ReadRequiredAsync(
            ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse,
            cancellationToken);
    }

    public async Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(
        string interfaceName,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceName);

        Uri uri = new(
            $"api/v1/network-interfaces/{Uri.EscapeDataString(interfaceName)}/comment",
            UriKind.Relative);
        UpdateNetworkInterfaceCommentRequest request = new() { Comment = comment };
        using JsonContent content = JsonContent.Create(
            request,
            ClientJsonSerializerContext.Default.UpdateNetworkInterfaceCommentRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(
            ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse,
            cancellationToken);
    }
}
