using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;

namespace Ufw.Web.Client.Features.Rules;

internal interface IRuleListProjectionService
{
    RuleListProjection Create(
        IReadOnlyList<ListedFirewallRule> rules,
        RuleOrderingPreview? orderingPreview,
        IReadOnlyDictionary<string, RuleMetadata>? metadataByRuleId = null);
}
