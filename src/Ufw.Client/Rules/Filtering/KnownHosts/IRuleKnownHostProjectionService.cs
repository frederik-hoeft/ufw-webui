using Ufw.Client.Api;

namespace Ufw.Client.Rules.Filtering.KnownHosts;

internal interface IRuleKnownHostProjectionService
{
    IReadOnlyList<RuleKnownHostProjection> Project(RuleRowProjection row, RuleFilterContext context);
}

internal sealed record RuleKnownHostProjection(RuleEndpointField Endpoint, KnownHostInventoryItem Host);
