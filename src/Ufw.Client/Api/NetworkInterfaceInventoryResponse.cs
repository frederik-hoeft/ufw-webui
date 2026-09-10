namespace Ufw.Client.Api;

public sealed class NetworkInterfaceInventoryResponse
{
    public IReadOnlyList<NetworkInterfaceInventoryItem> Interfaces { get; init; } = [];

    public DateTimeOffset? ReconciledAt { get; init; }
}
