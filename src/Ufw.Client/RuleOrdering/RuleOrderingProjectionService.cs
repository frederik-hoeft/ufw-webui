using Microsoft.Extensions.Localization;
using Ufw.Client.Localization;
using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

internal sealed class RuleOrderingProjectionService(IStringLocalizer<RulesStrings> rulesText) : IRuleOrderingProjectionService
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
                rulesText["OrderingTargetRange", rules.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)]);
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
                throw new InvalidOperationException(rulesText["OrderingAmbiguous"]);
            }

            sourceIndex = index;
        }

        if (sourceIndex < 0)
        {
            throw new InvalidOperationException(rulesText["OrderingMissing"]);
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
