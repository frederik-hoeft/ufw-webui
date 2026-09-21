using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal interface IRuleQueryService
{
    RuleFamilyQueryResult Evaluate(RuleFamilyProjection family, RuleQuery query, IReadOnlyList<KnownHostInventoryItem>? knownHosts = null);
}
