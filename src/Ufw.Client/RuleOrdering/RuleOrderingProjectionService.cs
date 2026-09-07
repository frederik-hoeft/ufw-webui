using Microsoft.Extensions.Localization;
using Ufw.Client.Api;
using Ufw.Client.Localization;
using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

internal sealed class RuleOrderingProjectionService(IStringLocalizer<RulesStrings> rulesText) : IRuleOrderingProjectionService
{
    public RuleOrderingPreview Move(
        IReadOnlyList<ListedFirewallRule> authoritativeRules,
        RuleOrderingPreview? currentPreview,
        RuleMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(authoritativeRules);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RuleId);

        IReadOnlyList<ListedFirewallRule> currentRules = currentPreview?.Rules ?? authoritativeRules;
        if (request.TargetPosition < 1 || request.TargetPosition > currentRules.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.TargetPosition,
                rulesText["OrderingTargetRange", currentRules.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)]);
        }

        int sourceIndex = FindUniqueRuleIndex(currentRules, request.RuleId);
        if (sourceIndex < 0)
        {
            throw new InvalidOperationException(rulesText["OrderingMissing"]);
        }

        List<ListedFirewallRule> ordered = [.. currentRules];
        ListedFirewallRule moved = ordered[sourceIndex];
        ordered.RemoveAt(sourceIndex);
        ordered.Insert(request.TargetPosition - 1, moved);

        Dictionary<string, int> originalPositions = currentPreview is null
            ? BuildUniqueOriginalPositions(authoritativeRules)
            : new Dictionary<string, int>(currentPreview.OriginalPositions, StringComparer.Ordinal);
        HashSet<string> directlyMovedRuleIds = currentPreview is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(currentPreview.DirectlyMovedRuleIds, StringComparer.Ordinal);
        directlyMovedRuleIds.Add(request.RuleId);

        List<RuleMoveRequest> moves = currentPreview is null ? [] : [.. currentPreview.Moves];
        moves.Add(request);

        ListedFirewallRule[] renumbered = ordered
            .Select((rule, index) => CopyWithDisplayNumber(rule, index + 1))
            .ToArray();

        return new RuleOrderingPreview(renumbered, originalPositions, directlyMovedRuleIds, moves);
    }

    private int FindUniqueRuleIndex(IReadOnlyList<ListedFirewallRule> rules, string ruleId)
    {
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

        return sourceIndex;
    }

    private static Dictionary<string, int> BuildUniqueOriginalPositions(IReadOnlyList<ListedFirewallRule> rules)
    {
        Dictionary<string, (int Position, int Count)> positions = new(StringComparer.Ordinal);
        for (int index = 0; index < rules.Count; index++)
        {
            string? ruleId = rules[index].RuleId;
            if (string.IsNullOrWhiteSpace(ruleId))
            {
                continue;
            }

            positions[ruleId] = positions.TryGetValue(ruleId, out (int Position, int Count) existing)
                ? (existing.Position, existing.Count + 1)
                : (index + 1, 1);
        }

        return positions
            .Where(static pair => pair.Value.Count == 1)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value.Position, StringComparer.Ordinal);
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
