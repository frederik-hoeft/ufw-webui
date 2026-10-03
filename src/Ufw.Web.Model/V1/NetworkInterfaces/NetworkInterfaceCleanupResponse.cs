using Ufw.Shared.Management.NetworkInterfaces;

namespace Ufw.Web.Model.V1.NetworkInterfaces;

public sealed class NetworkInterfaceCleanupResponse
{
    public NetworkInterfaceCleanupResponse() { }

    public NetworkInterfaceCleanupResponse(IReadOnlyList<NetworkInterfaceInventoryItem> staleInterfaces, DateTimeOffset? reconciledAt, int removedCount = 0) =>
        (StaleInterfaces, ReconciledAt, RemovedCount) = (staleInterfaces, reconciledAt, removedCount);

    public IReadOnlyList<NetworkInterfaceInventoryItem> StaleInterfaces { get; init; } = [];

    public DateTimeOffset? ReconciledAt { get; init; }

    public int RemovedCount { get; init; }
}
