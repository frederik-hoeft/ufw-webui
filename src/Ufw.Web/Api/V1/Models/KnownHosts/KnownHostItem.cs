using Ufw.Shared.Firewall;

namespace Ufw.Web.Api.V1.Models.KnownHosts;

public sealed record KnownHostItem(
    Guid Id,
    string Name,
    string Address,
    FirewallAddressFamily AddressFamily,
    string? Comment,
    bool IsVisible);
