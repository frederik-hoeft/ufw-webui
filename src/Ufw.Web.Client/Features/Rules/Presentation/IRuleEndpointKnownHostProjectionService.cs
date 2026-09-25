using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Presentation;

internal interface IRuleEndpointKnownHostProjectionService
{
    RuleEndpointKnownHostProjection Project(string? endpoint, IReadOnlyList<KnownHostInventoryItem> knownHosts);
}
