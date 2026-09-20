using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.RuleInsertion;

internal interface IRuleInsertionNavigationService
{
    string BuildUri(RuleListResponse baseline, ListedFirewallRule anchor, RuleInsertionPlacement placement);

    RuleInsertionNavigationResolution Resolve(RuleListResponse snapshot, RuleInsertionNavigationQuery query);
}
