using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Firewall;

internal static class FirewallRuleSnapshotMatcher
{
    public static bool Equivalent(RuleListResponse left, RuleListResponse right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Active != right.Active || left.Rules.Count != right.Rules.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Rules.Count; index++)
        {
            if (!FirewallRuleSemanticComparer.Equals(left.Rules[index], right.Rules[index]))
            {
                return false;
            }
        }
        return true;
    }

    public static bool TryMatchSingleInsertion(
        RuleListResponse baseline,
        RuleListResponse current,
        FirewallRuleSpecification inserted,
        int insertionIndex,
        out ListedFirewallRule? insertedRule)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(inserted);
        insertedRule = null;
        if (current.Active != baseline.Active || current.Rules.Count != baseline.Rules.Count + 1 || insertionIndex < 0 || insertionIndex >= current.Rules.Count)
        {
            return false;
        }

        for (int currentIndex = 0, baselineIndex = 0; currentIndex < current.Rules.Count; currentIndex++)
        {
            if (currentIndex == insertionIndex)
            {
                ListedFirewallRule candidate = current.Rules[currentIndex];
                if (candidate.Rule is null || !FirewallRuleSemanticComparer.Equals(candidate.Rule, inserted))
                {
                    return false;
                }
                insertedRule = candidate;
                continue;
            }

            if (baselineIndex >= baseline.Rules.Count || !FirewallRuleSemanticComparer.Equals(current.Rules[currentIndex], baseline.Rules[baselineIndex]))
            {
                return false;
            }
            baselineIndex++;
        }

        return insertedRule is not null;
    }

    public static bool TryMatchSingleReplacement(
        RuleListResponse baseline,
        RuleListResponse current,
        FirewallRuleSpecification replacement,
        int targetIndex,
        out ListedFirewallRule? replacementRule)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);
        replacementRule = null;
        if (current.Active != baseline.Active || current.Rules.Count != baseline.Rules.Count || targetIndex < 0 || targetIndex >= current.Rules.Count)
        {
            return false;
        }

        for (int index = 0; index < current.Rules.Count; index++)
        {
            if (index == targetIndex)
            {
                ListedFirewallRule candidate = current.Rules[index];
                if (candidate.Rule is null || !FirewallRuleSemanticComparer.Equals(candidate.Rule, replacement))
                {
                    return false;
                }
                replacementRule = candidate;
                continue;
            }

            if (!FirewallRuleSemanticComparer.Equals(current.Rules[index], baseline.Rules[index]))
            {
                return false;
            }
        }

        return replacementRule is not null;
    }
}
