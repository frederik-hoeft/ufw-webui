using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts.Model;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Features.Rules.Services;

internal interface IRulesPageProjectionService
{
    RulesPageProjection Create(RuleSnapshot? snapshot, RuleOrderingPreview? orderingPreview, RuleQuery query, IReadOnlyList<KnownHostInventoryItem> knownHosts);
}
