using Ufw.Client.RuleOrdering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules;

internal interface IRuleTableProjectionService
{
    RuleTableProjection Create(IReadOnlyList<ListedFirewallRule> rules, RuleOrderingPreview? orderingPreview);
}
