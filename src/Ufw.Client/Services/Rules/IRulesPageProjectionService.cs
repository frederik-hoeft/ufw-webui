using Ufw.Client.Api;
using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules;
using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Services.Rules;

internal interface IRulesPageProjectionService
{
    RulesPageProjection Create(RuleSnapshot? snapshot, RuleOrderingPreview? orderingPreview, RuleQuery query, IReadOnlyList<KnownHostInventoryItem> knownHosts);
}
