namespace Ufw.Web.Api.V1.Models.NetworkInterfaces;

public sealed record NetworkInterfaceInventoryResponse(IReadOnlyList<NetworkInterfaceInventoryItem> Interfaces, DateTimeOffset? ReconciledAt);
