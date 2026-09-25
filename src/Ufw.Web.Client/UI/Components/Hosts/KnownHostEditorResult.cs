using Ufw.Shared.Firewall;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Hosts;

internal sealed record KnownHostEditorResult(
    string Name,
    string? Address,
    KnownHostAddressSource AddressSource,
    FirewallAddressFamily? DnsAddressFamily,
    string? Comment,
    bool IsVisible);
