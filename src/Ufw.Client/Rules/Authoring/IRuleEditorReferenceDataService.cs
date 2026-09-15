using Ufw.Client.Api;

namespace Ufw.Client.Rules.Authoring;

internal interface IRuleEditorReferenceDataService
{
    Task<RuleEditorReferenceData> LoadAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<KnownHostInventoryItem> GetVisibleKnownHosts(RuleEditorReferenceData data, bool ipv6Enabled);

    bool IsUnknownInterface(RuleEditorReferenceData data, string? interfaceName);
}
