namespace Ufw.Web.Client.Features.NetworkInterfaces.Api;

public sealed class NetworkInterfaceInventoryResponse
{
    public IReadOnlyList<NetworkInterfaceInventoryItem> Interfaces { get; init; } = [];

    public DateTimeOffset? ReconciledAt { get; init; }
}
