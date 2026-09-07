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

    public async Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(
        string interfaceName,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.UpdateCommentAsync(interfaceName, comment, cancellationToken));
        return Current;
    }

    private static NetworkInterfaceInventoryResponse Normalize(NetworkInterfaceInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Interfaces is null || response.ReconciledAt == default)
        {
            throw new ApiProtocolException("Network-interface inventory response is missing required fields.");
        }

        NetworkInterfaceInventoryItem[] interfaces = response.Interfaces
            .Where(static entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Name))
            .Select(static entry => new NetworkInterfaceInventoryItem
            {
                Name = entry.Name.Trim(),
                Comment = string.IsNullOrWhiteSpace(entry.Comment) ? null : entry.Comment.Trim(),
            })
            .GroupBy(static entry => entry.Name, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static entry => entry.Name, StringComparer.Ordinal)
            .ToArray();

        return new NetworkInterfaceInventoryResponse
        {
            Interfaces = interfaces,
            ReconciledAt = response.ReconciledAt,
        };
    }
}
