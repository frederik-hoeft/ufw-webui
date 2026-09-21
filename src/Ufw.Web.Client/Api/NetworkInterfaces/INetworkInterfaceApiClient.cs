using Ufw.Web.Client.Api.NetworkInterfaces.Model;
namespace Ufw.Web.Client.Api.NetworkInterfaces;

public interface INetworkInterfaceApiClient
{
    Task<NetworkInterfaceInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(Guid interfaceId, string? comment, CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> UpdateVisibilityAsync(Guid interfaceId, bool isVisible, CancellationToken cancellationToken = default);
}
