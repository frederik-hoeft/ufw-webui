using Ufw.Shared.Management.NetworkInterfaces;

namespace Ufw.Web.Services.NetworkInterfaces;

public sealed record NetworkInterfaceCleanupResult(IReadOnlyList<NetworkInterfaceInventoryItem> StaleInterfaces, DateTimeOffset? ReconciledAt, int RemovedCount = 0);
