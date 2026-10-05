using Ufw.Shared.Management.NetworkInterfaces;

namespace Ufw.Web.Services.NetworkInterfaces;

public interface INetworkInterfaceInventoryService
{
    Task<NetworkInterfaceInventorySnapshot> GetCachedAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventorySnapshot> ReconcileAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventorySnapshot?> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventorySnapshot?> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default);

    Task<NetworkInterfaceCleanupResult> GetStaleAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceCleanupResult> CleanupStaleAsync(IReadOnlyCollection<Guid> interfaceIds, CancellationToken cancellationToken = default);
}
