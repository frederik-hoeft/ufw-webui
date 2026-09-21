using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts.Model;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Client.Api.NetworkInterfaces.Model;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed record RuleEditorReferenceData(
    IReadOnlyList<KnownHostInventoryItem> KnownHosts,
    IReadOnlyList<NetworkInterfaceInventoryItem> KnownInterfaces,
    IReadOnlyList<NetworkInterfaceInventoryItem> VisibleInterfaces,
    string? InterfaceInventoryError);
