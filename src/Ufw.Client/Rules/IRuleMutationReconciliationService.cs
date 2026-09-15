using Ufw.Client.RuleInsertion;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Rules;

internal interface IRuleMutationReconciliationService
{
    string GetRequestedIdentity(FirewallRuleSpecification requestedRule);

    string GetMutationIdentity(RuleMutationResponse response, string fallbackIdentity);

    bool IsPresent(RuleSnapshot snapshot, string identity);

    bool MustReselectInsertionAnchor(RuleInsertionResponse response, OrderedRuleInsertionNavigationContext context);
}
