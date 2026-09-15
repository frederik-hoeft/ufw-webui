using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.RuleOrdering;

internal interface IRuleOrderingResultProjectionService
{
    RuleOrderingResultProjection Create(RuleReorderResponse result, IReadOnlyList<ListedFirewallRule> baselineRules, IReadOnlyList<int> desiredOrder);
}
