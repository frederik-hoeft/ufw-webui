using Ufw.Web.Client.Features.KnownHosts.Api;
using Ufw.Web.Client.Features.NetworkInterfaces.Api;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed record RuleEditorReferenceData(
    IReadOnlyList<KnownHostInventoryItem> KnownHosts,
    IReadOnlyList<NetworkInterfaceInventoryItem> KnownInterfaces,
    IReadOnlyList<NetworkInterfaceInventoryItem> VisibleInterfaces,
    string? InterfaceInventoryError);
