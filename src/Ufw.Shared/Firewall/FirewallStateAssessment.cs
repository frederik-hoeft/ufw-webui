using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Shared.Firewall;

/// <summary>
/// Identifies ambiguity in an authoritative firewall snapshot that makes mutations unsafe.
/// </summary>
public static class FirewallStateAssessmentEvaluator
{
    public const string DUPLICATE_RULE_IDENTITY = "firewall.state.duplicate-rule-identity";

    public static FirewallStateAssessment Evaluate(IReadOnlyList<ListedFirewallRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Dictionary<string, List<int>> occurrences = new(StringComparer.Ordinal);
        for (int index = 0; index < rules.Count; index++)
        {
            string? ruleId = rules[index].RuleId;
            if (ruleId is null)
            {
                continue;
            }

            if (!occurrences.TryGetValue(ruleId, out List<int>? matches))
            {
                matches = [];
                occurrences.Add(ruleId, matches);
            }
            matches.Add(index);
        }

        FirewallStateIssue[] issues = [.. occurrences
            .Where(static pair => pair.Value.Count > 1)
            .OrderBy(static pair => pair.Value[0])
            .Select(static pair => new FirewallStateIssue(DUPLICATE_RULE_IDENTITY, pair.Key, pair.Value.ToArray()))];
        return new FirewallStateAssessment(issues);
    }
}
