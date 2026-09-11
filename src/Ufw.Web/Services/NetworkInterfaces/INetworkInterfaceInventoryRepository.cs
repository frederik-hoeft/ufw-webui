using Ufw.Web.Api.V1.Models.NetworkInterfaces;

namespace Ufw.Web.Services.NetworkInterfaces;

internal interface INetworkInterfaceInventoryRepository
{
    Task<NetworkInterfaceInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> ReconcileAsync(IReadOnlyList<string> currentNames, DateTimeOffset reconciledAt, CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse?> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse?> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default);
}
