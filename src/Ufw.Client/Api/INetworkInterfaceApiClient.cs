namespace Ufw.Client.Api;

public interface INetworkInterfaceApiClient
{
    bool UsesMockData { get; }

    Task<NetworkInterfaceInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default);

    Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(
        string interfaceName,
        string? comment,
        CancellationToken cancellationToken = default);
}
