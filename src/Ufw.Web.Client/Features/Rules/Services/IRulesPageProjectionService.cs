using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Services;

internal interface IRulesPageProjectionService
{
    RulesPageProjection Create(RuleSnapshot? snapshot, RuleOrderingPreview? orderingPreview, RuleQuery query, IReadOnlyList<KnownHostInventoryItem> knownHosts);
}
