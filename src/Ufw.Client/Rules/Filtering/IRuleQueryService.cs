using Ufw.Client.Api;

namespace Ufw.Client.Rules.Filtering;

internal interface IRuleQueryService
{
    RuleFamilyQueryResult Evaluate(RuleFamilyProjection family, RuleQuery query, IReadOnlyList<KnownHostInventoryItem>? knownHosts = null);
}
