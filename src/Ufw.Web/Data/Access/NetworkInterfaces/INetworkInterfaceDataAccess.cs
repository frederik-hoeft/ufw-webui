using Ufw.Shared.Management.NetworkInterfaces;

namespace Ufw.Web.Data.Access.NetworkInterfaces;

internal interface INetworkInterfaceDataAccess
{
    Task<NetworkInterfaceInventorySnapshot> GetPresentAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventorySnapshot> GetStaleAsync(CancellationToken cancellationToken = default);

    Task ReconcileAsync(IReadOnlyList<string> currentNames, DateTimeOffset reconciledAt, CancellationToken cancellationToken = default);

    Task<int> ReconcileAndDeleteStaleAsync(
        IReadOnlyList<string> currentNames,
        IReadOnlyCollection<Guid> selectedIds,
        DateTimeOffset reconciledAt,
        CancellationToken cancellationToken = default);

    Task<DataMutationResult> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default);

    Task<DataMutationResult> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default);
}
