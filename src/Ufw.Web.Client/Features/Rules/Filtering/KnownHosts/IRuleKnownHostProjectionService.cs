using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Filtering.KnownHosts;

internal interface IRuleKnownHostProjectionService
{
    IReadOnlyList<RuleKnownHostProjection> Project(RuleRowProjection row, RuleFilterContext context);
}

internal sealed record RuleKnownHostProjection(RuleEndpointField Endpoint, KnownHostInventoryItem Host);
