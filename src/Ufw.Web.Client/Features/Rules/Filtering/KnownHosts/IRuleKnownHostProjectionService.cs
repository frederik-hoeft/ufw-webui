using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts.Model;

namespace Ufw.Web.Client.Features.Rules.Filtering.KnownHosts;

internal interface IRuleKnownHostProjectionService
{
    IReadOnlyList<RuleKnownHostProjection> Project(RuleRowProjection row, RuleFilterContext context);
}

internal sealed record RuleKnownHostProjection(RuleEndpointField Endpoint, KnownHostInventoryItem Host);
