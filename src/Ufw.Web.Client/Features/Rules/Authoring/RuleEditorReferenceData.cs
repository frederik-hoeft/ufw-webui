using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Model.V1.NetworkInterfaces;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed record RuleEditorReferenceData(
    IReadOnlyList<KnownHostInventoryItem> KnownHosts,
    IReadOnlyList<NetworkInterfaceInventoryItem> KnownInterfaces,
    IReadOnlyList<NetworkInterfaceInventoryItem> VisibleInterfaces,
    string? InterfaceInventoryError);
