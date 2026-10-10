using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal interface IRuleEditorReferenceDataService
{
    /// <summary>
    /// Loads known-host and network-interface suggestions independently. Expected failures are retained per catalog; cancellation and unclassified failures propagate.
    /// </summary>
    Task<RuleEditorReferenceData> LoadAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<KnownHostInventoryItem> GetVisibleKnownHosts(RuleEditorReferenceData data, bool ipv6Enabled);

    bool IsUnknownInterface(RuleEditorReferenceData data, string? interfaceName);
}
