using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Ordering;

internal interface IRuleOrderingProjectionService
{
    RuleOrderingPreview Move(IReadOnlyList<ListedFirewallRule> authoritativeRules, RuleOrderingPreview? currentPreview, RuleMoveRequest request);
}
