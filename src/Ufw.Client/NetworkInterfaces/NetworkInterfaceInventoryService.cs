using Ufw.Client.Api;

namespace Ufw.Client.NetworkInterfaces;

internal sealed class NetworkInterfaceInventoryService(INetworkInterfaceApiClient apiClient) : INetworkInterfaceInventoryService
{
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

    public async Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(Guid interfaceId, string? comment, CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.UpdateCommentAsync(interfaceId, comment, cancellationToken));
        return Current;
    }

    public async Task<NetworkInterfaceInventoryResponse> UpdateVisibilityAsync(Guid interfaceId, bool isVisible, CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.UpdateVisibilityAsync(interfaceId, isVisible, cancellationToken));
        return Current;
    }

    private static NetworkInterfaceInventoryResponse Normalize(NetworkInterfaceInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Interfaces is null)
        {
            throw new ApiProtocolException("Network-interface inventory response is missing the interface list.");
        }

        if (response.Interfaces.Any(static entry =>
            entry is null || entry.Id == Guid.Empty || string.IsNullOrWhiteSpace(entry.Name)))
        {
            throw new ApiProtocolException("Network-interface inventory response contains an invalid interface entry.");
        }

        NetworkInterfaceInventoryItem[] interfaces =
        [
            .. response.Interfaces
            .Select(static entry => new NetworkInterfaceInventoryItem
            {
                Id = entry.Id,
                Name = entry.Name,
                Comment = string.IsNullOrWhiteSpace(entry.Comment) ? null : entry.Comment.Trim(),
                IsVisible = entry.IsVisible,
            })
            .OrderByDescending(static entry => entry.IsVisible)
            .ThenBy(static entry => entry.Name, StringComparer.Ordinal)
        ];

        if (interfaces.Select(static entry => entry.Id).Distinct().Count() != interfaces.Length
            || interfaces.Select(static entry => entry.Name).Distinct(StringComparer.Ordinal).Count() != interfaces.Length)
        {
            throw new ApiProtocolException("Network-interface inventory response contains duplicate interface identities.");
        }

        return new NetworkInterfaceInventoryResponse
        {
            Interfaces = interfaces,
            ReconciledAt = response.ReconciledAt,
        };
    }
}
