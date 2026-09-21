namespace Ufw.Web.Client.Api.KnownHosts.Model;

public sealed class KnownHostInventoryResponse
{
    public IReadOnlyList<KnownHostInventoryItem> Hosts { get; init; } = [];
}
