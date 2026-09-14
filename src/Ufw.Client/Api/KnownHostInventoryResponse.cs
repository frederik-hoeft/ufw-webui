namespace Ufw.Client.Api;

public sealed class KnownHostInventoryResponse
{
    public IReadOnlyList<KnownHostInventoryItem> Hosts { get; init; } = [];
}
