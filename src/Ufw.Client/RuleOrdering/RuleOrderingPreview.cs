using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

public sealed class RuleOrderingPreview
{
    private readonly Dictionary<ListedFirewallRule, int> _occurrenceIds;

    public RuleOrderingPreview(
        IReadOnlyList<ListedFirewallRule> rules,
        IReadOnlyList<int> desiredOrder,
        IReadOnlySet<int> directlyMovedOccurrences)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(desiredOrder);
        ArgumentNullException.ThrowIfNull(directlyMovedOccurrences);
        if (rules.Count != desiredOrder.Count)
        {
            throw new ArgumentException("The projected rules and desired order must have the same number of entries.", nameof(desiredOrder));
        }

        Rules = rules;
        DesiredOrder = desiredOrder;
        DirectlyMovedOccurrences = directlyMovedOccurrences;
        HasChanges = false;
        _occurrenceIds = new Dictionary<ListedFirewallRule, int>(rules.Count, ReferenceEqualityComparer.Instance);
        for (int index = 0; index < rules.Count; index++)
        {
            _occurrenceIds.Add(rules[index], desiredOrder[index]);
            HasChanges |= desiredOrder[index] != index;
        }
    }

    public IReadOnlyList<ListedFirewallRule> Rules { get; }

    public IReadOnlyList<int> DesiredOrder { get; }

    public IReadOnlySet<int> DirectlyMovedOccurrences { get; }

    public bool HasChanges { get; }

    public int? GetOccurrenceId(ListedFirewallRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return _occurrenceIds.TryGetValue(rule, out int occurrenceId) ? occurrenceId : null;
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
}
