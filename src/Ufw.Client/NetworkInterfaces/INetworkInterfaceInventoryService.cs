using Ufw.Client.Api;

namespace Ufw.Client.NetworkInterfaces;

public interface INetworkInterfaceInventoryService
{
    bool UsesMockData { get; }

    NetworkInterfaceInventoryResponse? Current { get; }

    Task<NetworkInterfaceInventoryResponse> RefreshAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(
        string interfaceName,
        string? comment,
        CancellationToken cancellationToken = default);
}
