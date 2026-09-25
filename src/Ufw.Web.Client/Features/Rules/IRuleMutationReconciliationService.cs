using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Insertion;

namespace Ufw.Web.Client.Features.Rules;

internal interface IRuleMutationReconciliationService
{
    string GetRequestedIdentity(FirewallRuleSpecification requestedRule);

    string GetMutationIdentity(RuleMutationResponse response, string fallbackIdentity);

    bool IsPresent(RuleSnapshot snapshot, string identity);

    bool MustReselectInsertionAnchor(RuleInsertionResponse response, OrderedRuleInsertionNavigationContext context);
}
