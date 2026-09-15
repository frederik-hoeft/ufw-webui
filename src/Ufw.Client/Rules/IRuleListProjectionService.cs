using Ufw.Client.RuleOrdering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules;

internal interface IRuleListProjectionService
{
    RuleListProjection Create(IReadOnlyList<ListedFirewallRule> rules, RuleOrderingPreview? orderingPreview);
}
