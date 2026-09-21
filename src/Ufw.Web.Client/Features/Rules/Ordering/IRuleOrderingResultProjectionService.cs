using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules.Ordering;

internal interface IRuleOrderingResultProjectionService
{
    RuleOrderingResultProjection Create(RuleReorderResponse result, IReadOnlyList<ListedFirewallRule> baselineRules, IReadOnlyList<int> desiredOrder);
}
