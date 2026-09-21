namespace Ufw.Web.Client.Features.KnownHosts.Api;

public sealed class KnownHostInventoryResponse
{
    public IReadOnlyList<KnownHostInventoryItem> Hosts { get; init; } = [];
}
