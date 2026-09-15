namespace Ufw.Client.RuleOrdering;

public sealed class RuleOrderingPreview
{
    public RuleOrderingPreview(IReadOnlyList<int> desiredOrder, IReadOnlySet<int> directlyMovedOccurrences)
    {
        ArgumentNullException.ThrowIfNull(desiredOrder);
        ArgumentNullException.ThrowIfNull(directlyMovedOccurrences);

        int[] order = [.. desiredOrder];
        ValidatePermutation(order);

        HashSet<int> directlyMoved = [.. directlyMovedOccurrences];
        if (directlyMoved.Any(occurrenceId => occurrenceId < 0 || occurrenceId >= order.Length))
        {
            throw new ArgumentOutOfRangeException(nameof(directlyMovedOccurrences), "Directly moved occurrence IDs must refer to the ordering baseline.");
        }

        DesiredOrder = order;
        DirectlyMovedOccurrences = directlyMoved;
        HasChanges = order.Where((occurrenceId, index) => occurrenceId != index).Any();
    }

    public IReadOnlyList<int> DesiredOrder { get; }

    public IReadOnlySet<int> DirectlyMovedOccurrences { get; }

    public bool HasChanges { get; }

    public bool WasDirectlyMoved(int occurrenceId) => DirectlyMovedOccurrences.Contains(occurrenceId);

    private static void ValidatePermutation(IReadOnlyList<int> desiredOrder)
    {
        bool[] observed = new bool[desiredOrder.Count];
        for (int index = 0; index < desiredOrder.Count; index++)
        {
            int occurrenceId = desiredOrder[index];
            if (occurrenceId < 0 || occurrenceId >= desiredOrder.Count || observed[occurrenceId])
            {
                throw new ArgumentException("The desired order must contain each ordering-baseline occurrence exactly once.", nameof(desiredOrder));
            }

            observed[occurrenceId] = true;
        }
    }
}
