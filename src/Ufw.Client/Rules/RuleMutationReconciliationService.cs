using Ufw.Client.RuleInsertion;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Rules;

internal sealed class RuleMutationReconciliationService : IRuleMutationReconciliationService
{
    public string GetRequestedIdentity(FirewallRuleSpecification requestedRule)
    {
        ArgumentNullException.ThrowIfNull(requestedRule);
        return RuleIdentity.Compute(requestedRule);
    }

    public string GetMutationIdentity(RuleMutationResponse response, string fallbackIdentity)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackIdentity);
        return GetListedRuleIdentity(response.Rule) ?? fallbackIdentity;
    }

    public bool IsPresent(RuleSnapshot snapshot, string identity)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);

        return snapshot.Rules.Any(rule => rule.Parsed
            && rule.Rule is not null
            && string.Equals(rule.RuleId ?? RuleIdentity.Compute(rule.Rule), identity, StringComparison.Ordinal));
    }

    public bool MustReselectInsertionAnchor(RuleInsertionResponse response, OrderedRuleInsertionNavigationContext context)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(context);

        if (response.Outcome is RuleInsertionOutcome.StaleBaseline or RuleInsertionOutcome.StateUncertain || response.FinalSnapshot is null)
        {
            return true;
        }

        return !string.Equals(FirewallRuleSnapshotFingerprint.Compute(response.FinalSnapshot), context.BaselineFingerprint, StringComparison.Ordinal);
    }

    private static string? GetListedRuleIdentity(ListedFirewallRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.RuleId))
        {
            return rule.RuleId;
        }

        return rule.Parsed && rule.Rule is not null ? RuleIdentity.Compute(rule.Rule) : null;
    }
}
