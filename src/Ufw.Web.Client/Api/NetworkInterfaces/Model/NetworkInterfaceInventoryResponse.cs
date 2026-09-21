namespace Ufw.Web.Client.Api.NetworkInterfaces.Model;

public sealed class NetworkInterfaceInventoryResponse
{
    public IReadOnlyList<NetworkInterfaceInventoryItem> Interfaces { get; init; } = [];

    public DateTimeOffset? ReconciledAt { get; init; }
}
