using Ufw.Shared.Management.KnownHosts;
using Ufw.Shared.Management.NetworkInterfaces;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed record RuleEditorReferenceData(
    RuleEditorCatalogResult<KnownHostInventoryItem> KnownHosts,
    RuleEditorCatalogResult<NetworkInterfaceInventoryItem> Interfaces)
{
    public static RuleEditorReferenceData Empty { get; } = new(RuleEditorCatalogResult<KnownHostInventoryItem>.Loaded([]), RuleEditorCatalogResult<NetworkInterfaceInventoryItem>.Loaded([]));

    public IReadOnlyList<NetworkInterfaceInventoryItem> VisibleInterfaces { get; } =
        Interfaces.Items.Where(static networkInterface => networkInterface.IsVisible).ToArray();
}
