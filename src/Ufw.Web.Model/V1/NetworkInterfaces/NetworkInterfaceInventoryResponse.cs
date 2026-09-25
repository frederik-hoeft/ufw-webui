namespace Ufw.Web.Model.V1.NetworkInterfaces;

public sealed class NetworkInterfaceInventoryResponse
{
    public NetworkInterfaceInventoryResponse() { }

    public NetworkInterfaceInventoryResponse(IReadOnlyList<NetworkInterfaceInventoryItem> interfaces, DateTimeOffset? reconciledAt) =>
        (Interfaces, ReconciledAt) = (interfaces, reconciledAt);

    public IReadOnlyList<NetworkInterfaceInventoryItem> Interfaces { get; init; } = [];

    public DateTimeOffset? ReconciledAt { get; init; }
}
