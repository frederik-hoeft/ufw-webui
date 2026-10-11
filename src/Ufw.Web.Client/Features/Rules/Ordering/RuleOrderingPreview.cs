namespace Ufw.Web.Client.Features.Rules.Ordering;

public sealed class RuleOrderingPreview
{
    public RuleOrderingPreview(IReadOnlyList<int> desiredOrder, IReadOnlySet<int> directlyMovedOccurrences)
    {
        ArgumentNullException.ThrowIfNull(desiredOrder);
        ArgumentNullException.ThrowIfNull(directlyMovedOccurrences);

        RuleOrderPermutation order = RuleOrderPermutation.Create(desiredOrder, desiredOrder.Count);

        HashSet<int> directlyMoved = [.. directlyMovedOccurrences];
        if (directlyMoved.Any(occurrenceId => occurrenceId < 0 || occurrenceId >= order.Occurrences.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(directlyMovedOccurrences), "Directly moved occurrence IDs must refer to the ordering baseline.");
        }

        DesiredOrder = order.Occurrences;
        DirectlyMovedOccurrences = directlyMoved;
        HasChanges = order.Occurrences.Where((occurrenceId, index) => occurrenceId != index).Any();
    }

    public IReadOnlyList<int> DesiredOrder { get; }

    public IReadOnlySet<int> DirectlyMovedOccurrences { get; }

    public bool HasChanges { get; }

    public bool WasDirectlyMoved(int occurrenceId) => DirectlyMovedOccurrences.Contains(occurrenceId);
}
