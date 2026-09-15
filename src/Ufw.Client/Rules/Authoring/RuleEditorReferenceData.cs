using Ufw.Client.Api;

namespace Ufw.Client.Rules.Authoring;

internal sealed record RuleEditorReferenceData(
    IReadOnlyList<KnownHostInventoryItem> KnownHosts,
    IReadOnlyList<NetworkInterfaceInventoryItem> KnownInterfaces,
    IReadOnlyList<NetworkInterfaceInventoryItem> VisibleInterfaces,
    string? InterfaceInventoryError);
