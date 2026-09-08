namespace Ufw.Client.Api;

public sealed class NetworkInterfaceInventoryItem
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Comment { get; init; }
}

public sealed class NetworkInterfaceInventoryResponse
{
    public IReadOnlyList<NetworkInterfaceInventoryItem> Interfaces { get; init; } = [];

    public DateTimeOffset? ReconciledAt { get; init; }
}

public sealed class UpdateNetworkInterfaceCommentRequest
{
    public string? Comment { get; init; }
}
