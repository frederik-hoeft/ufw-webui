using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules.Replacement;

internal interface IRuleReplacementNavigationService
{
    string BuildUri(RuleListResponse baseline, int targetOccurrenceId);

    RuleReplacementNavigationResolution Resolve(RuleListResponse snapshot, RuleReplacementNavigationQuery query);
}
