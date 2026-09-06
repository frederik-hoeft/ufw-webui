using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

internal sealed class RuleOrderingProjectionService : IRuleOrderingProjectionService
{
    public IReadOnlyList<ListedFirewallRule> Move(
        IReadOnlyList<ListedFirewallRule> rules,
        string ruleId,
        int targetPosition)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        if (targetPosition < 1 || targetPosition > rules.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetPosition),
                targetPosition,
                $"Target position must be between 1 and {rules.Count}.");
        }

        int sourceIndex = -1;
        for (int index = 0; index < rules.Count; index++)
        {
            if (!string.Equals(rules[index].RuleId, ruleId, StringComparison.Ordinal))
            {
                continue;
            }

            if (sourceIndex >= 0)
            {
                throw new InvalidOperationException("The rule ID is ambiguous in the current snapshot.");
            }

            sourceIndex = index;
        }

        if (sourceIndex < 0)
        {
            throw new InvalidOperationException("The rule is not present in the current snapshot.");
        }

        List<ListedFirewallRule> ordered = [.. rules];
        ListedFirewallRule moved = ordered[sourceIndex];
        ordered.RemoveAt(sourceIndex);
        ordered.Insert(targetPosition - 1, moved);

        return ordered
            .Select((rule, index) => CopyWithDisplayNumber(rule, index + 1))
            .ToArray();
    }

    private static ListedFirewallRule CopyWithDisplayNumber(ListedFirewallRule rule, int displayNumber) => new()
    {
        RuleId = rule.RuleId,
        DisplayNumber = displayNumber,
        Parsed = rule.Parsed,
        RawLine = rule.RawLine,
        Rule = rule.Rule,
    };
}
