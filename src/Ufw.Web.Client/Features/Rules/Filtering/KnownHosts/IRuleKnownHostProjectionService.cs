using Ufw.Web.Client.Features.KnownHosts.Api;

namespace Ufw.Web.Client.Features.Rules.Filtering.KnownHosts;

internal interface IRuleKnownHostProjectionService
{
    IReadOnlyList<RuleKnownHostProjection> Project(RuleRowProjection row, RuleFilterContext context);
}

internal sealed record RuleKnownHostProjection(RuleEndpointField Endpoint, KnownHostInventoryItem Host);
