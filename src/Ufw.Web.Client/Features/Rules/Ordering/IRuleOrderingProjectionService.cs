using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Api;

namespace Ufw.Web.Client.Features.Rules.Ordering;

internal interface IRuleOrderingProjectionService
{
    RuleOrderingPreview Move(IReadOnlyList<ListedFirewallRule> authoritativeRules, RuleOrderingPreview? currentPreview, RuleMoveRequest request);
}
