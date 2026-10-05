namespace Ufw.Shared.Management.NetworkInterfaces;

public sealed record NetworkInterfaceInventorySnapshot(IReadOnlyList<NetworkInterfaceInventoryItem> Interfaces, DateTimeOffset? ReconciledAt);
