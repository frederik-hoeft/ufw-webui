using Ufw.Web.Model.V1.NetworkInterfaces;

namespace Ufw.Web.Services.NetworkInterfaces;

public interface INetworkInterfaceInventoryService
{
    Task<NetworkInterfaceInventoryResponse> GetCachedAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse?> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse?> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default);
}
