using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

public sealed class RuleOrderingPreview
(
    IReadOnlyList<ListedFirewallRule> rules,
    IReadOnlyList<int> desiredOrder,
    IReadOnlySet<int> directlyMovedOccurrences
)
{
    public IReadOnlyList<ListedFirewallRule> Rules { get; } = rules;

    public IReadOnlyList<int> DesiredOrder { get; } = desiredOrder;

    public IReadOnlySet<int> DirectlyMovedOccurrences { get; } = directlyMovedOccurrences;

    public bool HasChanges => DesiredOrder.Where((occurrenceId, index) => occurrenceId != index).Any();

    public int? GetOccurrenceId(ListedFirewallRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        int index = IndexOf(rule);
        return index >= 0 ? DesiredOrder[index] : null;
    }

    public int? GetOriginalPosition(ListedFirewallRule rule)
    {
        int? occurrenceId = GetOccurrenceId(rule);
        return occurrenceId is null ? null : occurrenceId.Value + 1;
    }

    public bool WasDirectlyMoved(ListedFirewallRule rule)
    {
        int? occurrenceId = GetOccurrenceId(rule);
        return occurrenceId is not null && DirectlyMovedOccurrences.Contains(occurrenceId.Value);
    }

    private int IndexOf(ListedFirewallRule rule)
    {
        for (int index = 0; index < Rules.Count; index++)
        {
            if (ReferenceEquals(Rules[index], rule))
            {
                return index;
            }
        }

        return -1;
    }
}
