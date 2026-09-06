using Ufw.Client.Api;

namespace Ufw.Client.NetworkInterfaces;

internal sealed class NetworkInterfaceInventoryService(INetworkInterfaceApiClient apiClient) : INetworkInterfaceInventoryService
{
    public bool UsesMockData => apiClient.UsesMockData;

    public NetworkInterfaceInventoryResponse? Current { get; private set; }

    public async Task<NetworkInterfaceInventoryResponse> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.GetAsync(cancellationToken));
        return Current;
    }

    public async Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.ReconcileAsync(cancellationToken));
        return Current;
    }

    private static NetworkInterfaceInventoryResponse Normalize(NetworkInterfaceInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Interfaces is null || response.ReconciledAt == default)
        {
            throw new ApiProtocolException("Network-interface inventory response is missing required fields.");
        }

        string[] interfaces = response.Interfaces
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new NetworkInterfaceInventoryResponse
        {
            Interfaces = interfaces,
            ReconciledAt = response.ReconciledAt,
        };
    }
}
