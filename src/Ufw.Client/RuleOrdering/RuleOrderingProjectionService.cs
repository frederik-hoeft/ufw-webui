using Microsoft.Extensions.Localization;
using Ufw.Client.Api;
using Ufw.Client.Localization;
using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

internal sealed class RuleOrderingProjectionService(IStringLocalizer<RulesStrings> rulesText) : IRuleOrderingProjectionService
{
    public RuleOrderingPreview Move(IReadOnlyList<ListedFirewallRule> authoritativeRules, RuleOrderingPreview? currentPreview, RuleMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(authoritativeRules);
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<int> currentOrder = currentPreview?.DesiredOrder ?? Enumerable.Range(0, authoritativeRules.Count).ToArray();
        if (currentOrder.Count != authoritativeRules.Count)
        {
            throw new InvalidOperationException(rulesText["OrderingBaselineChanged"]);
        }

        if (request.TargetPosition < 1 || request.TargetPosition > currentOrder.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.TargetPosition,
                rulesText["OrderingTargetRange", currentOrder.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)]);
        }

        int sourceIndex = IndexOfOccurrence(currentOrder, request.OccurrenceId);
        if (sourceIndex < 0)
        {
            throw new InvalidOperationException(rulesText["OrderingMissing"]);
        }

        List<int> desiredOrder = [.. currentOrder];
        desiredOrder.RemoveAt(sourceIndex);
        desiredOrder.Insert(request.TargetPosition - 1, request.OccurrenceId);

        HashSet<int> directlyMovedOccurrences = currentPreview is null
            ? []
            : [.. currentPreview.DirectlyMovedOccurrences];
        directlyMovedOccurrences.Add(request.OccurrenceId);

        ListedFirewallRule[] projected = [.. desiredOrder.Select((occurrenceId, index) =>
            CopyWithDisplayNumber(authoritativeRules[occurrenceId], index + 1))];

        return new RuleOrderingPreview(projected, desiredOrder, directlyMovedOccurrences);
    }

    private static int IndexOfOccurrence(IReadOnlyList<int> order, int occurrenceId)
    {
        for (int index = 0; index < order.Count; index++)
        {
            if (order[index] == occurrenceId)
            {
                return index;
            }
        }

        return -1;
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
