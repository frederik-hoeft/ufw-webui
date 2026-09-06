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
            Serialization.ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse,
            cancellationToken);
    }

    public async Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(s_reconcileUri, content: null, cancellationToken);
        return await response.ReadRequiredAsync(
            Serialization.ClientJsonSerializerContext.Default.NetworkInterfaceInventoryResponse,
            cancellationToken);
    }
}
