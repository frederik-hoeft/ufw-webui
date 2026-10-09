using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules.Insertion;

internal interface IRuleInsertionNavigationService
{
    string BuildUri(RuleListResponse baseline, int anchorOccurrenceId, RuleInsertionPlacement placement);

    RuleInsertionNavigationResolution Resolve(RuleListResponse snapshot, RuleInsertionNavigationQuery query);
}
