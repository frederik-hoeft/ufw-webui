namespace Ufw.Web.Api.V1.Models.KnownHosts;

public sealed record KnownHostInventoryResponse(IReadOnlyList<KnownHostItem> Hosts);
