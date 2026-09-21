using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.KnownHosts.Api;

namespace Ufw.Web.Client.Features.KnownHosts;

internal interface IKnownHostSuggestionService
{
    IEnumerable<KnownHostInventoryItem> Search(IReadOnlyList<KnownHostInventoryItem> suggestions, FirewallAddressFamily addressFamily, string? query);

    FirewallAddressFamily ResolveCompatibleAddressFamily(FirewallAddressFamily declaredFamily, string? oppositeAddress);

    string GetSelectionValue(KnownHostInventoryItem host);

    KnownHostInventoryItem? ResolveSelectionValue(IReadOnlyList<KnownHostInventoryItem> suggestions, FirewallAddressFamily addressFamily, string? selectionValue);
}
