using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules.Metadata;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules;

internal interface IRuleListProjectionService
{
    RuleListProjection Create(
        IReadOnlyList<ListedFirewallRule> rules,
        RuleOrderingPreview? orderingPreview,
        IReadOnlyDictionary<string, RuleMetadata>? metadataByRuleId = null);
}
