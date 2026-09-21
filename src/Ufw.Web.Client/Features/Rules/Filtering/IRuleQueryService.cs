using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts.Model;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal interface IRuleQueryService
{
    RuleFamilyQueryResult Evaluate(RuleFamilyProjection family, RuleQuery query, IReadOnlyList<KnownHostInventoryItem>? knownHosts = null);
}
