namespace Ufw.Web.Model.V1.KnownHosts;

public sealed class KnownHostInventoryResponse
{
    public KnownHostInventoryResponse() { }

    public KnownHostInventoryResponse(IReadOnlyList<KnownHostInventoryItem> hosts) => Hosts = hosts;

    public IReadOnlyList<KnownHostInventoryItem> Hosts { get; init; } = [];
}
