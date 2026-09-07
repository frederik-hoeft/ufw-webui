using Ufw.Client.Api;
using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

internal interface IRuleOrderingProjectionService
{
    RuleOrderingPreview Move(
        IReadOnlyList<ListedFirewallRule> authoritativeRules,
        RuleOrderingPreview? currentPreview,
        RuleMoveRequest request);
}
