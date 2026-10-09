namespace Ufw.Web.Client.Features.Rules.Ordering;

/// <summary>
/// A validated, immutable permutation of all zero-based occurrences in one authoritative rule snapshot.
/// The caller must still associate the permutation with the corresponding signed baseline fingerprint.
/// </summary>
internal sealed class RuleOrderPermutation
{
    private RuleOrderPermutation(int[] occurrences) => Occurrences = Array.AsReadOnly(occurrences);

    public IReadOnlyList<int> Occurrences { get; }

    public static RuleOrderPermutation Create(IReadOnlyList<int> desiredOrder, int occurrenceCount)
    {
        ArgumentNullException.ThrowIfNull(desiredOrder);
        if (occurrenceCount < 0 || desiredOrder.Count != occurrenceCount)
        {
            throw new ArgumentException("The desired order must contain every baseline occurrence exactly once.", nameof(desiredOrder));
        }

        bool[] seen = new bool[occurrenceCount];
        int[] validated = new int[occurrenceCount];
        for (int index = 0; index < occurrenceCount; index++)
        {
            int occurrenceId = desiredOrder[index];
            if (occurrenceId < 0 || occurrenceId >= occurrenceCount || seen[occurrenceId])
            {
                throw new ArgumentException("The desired order must be a permutation of all baseline occurrences.", nameof(desiredOrder));
            }

            seen[occurrenceId] = true;
            validated[index] = occurrenceId;
        }

        return new RuleOrderPermutation(validated);
    }
}
