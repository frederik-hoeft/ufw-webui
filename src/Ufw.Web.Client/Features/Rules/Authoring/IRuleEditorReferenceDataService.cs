using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts.Model;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal interface IRuleEditorReferenceDataService
{
    Task<RuleEditorReferenceData> LoadAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<KnownHostInventoryItem> GetVisibleKnownHosts(RuleEditorReferenceData data, bool ipv6Enabled);

    bool IsUnknownInterface(RuleEditorReferenceData data, string? interfaceName);
}
