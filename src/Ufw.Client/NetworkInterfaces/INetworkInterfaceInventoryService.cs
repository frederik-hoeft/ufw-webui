using Ufw.Client.Api;

namespace Ufw.Client.NetworkInterfaces;

public interface INetworkInterfaceInventoryService
{
    NetworkInterfaceInventoryResponse? Current { get; }

    Task<NetworkInterfaceInventoryResponse> RefreshAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(
        Guid interfaceId,
        string? comment,
        CancellationToken cancellationToken = default);
}
