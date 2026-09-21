using Ufw.Web.Client.Features.KnownHosts.Api;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal interface IRuleQueryService
{
    RuleFamilyQueryResult Evaluate(RuleFamilyProjection family, RuleQuery query, IReadOnlyList<KnownHostInventoryItem>? knownHosts = null);
}
