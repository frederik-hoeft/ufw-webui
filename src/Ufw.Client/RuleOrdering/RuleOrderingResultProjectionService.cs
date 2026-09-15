using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.RuleOrdering;

internal sealed class RuleOrderingResultProjectionService : IRuleOrderingResultProjectionService
{
    public RuleOrderingResultProjection Create(
        RuleReorderResponse result,
        IReadOnlyList<ListedFirewallRule> baselineRules,
        IReadOnlyList<int> desiredOrder)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(baselineRules);
        ArgumentNullException.ThrowIfNull(desiredOrder);
        ValidateDesiredOrder(desiredOrder, baselineRules.Count);

        FirewallAddressFamily[] families = new FirewallAddressFamily[baselineRules.Count];
        int[] baselineFamilyPositions = new int[baselineRules.Count];
        Dictionary<FirewallAddressFamily, int> familyCounts = new();
        for (int occurrenceId = 0; occurrenceId < baselineRules.Count; occurrenceId++)
        {
            FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(baselineRules[occurrenceId]);
            families[occurrenceId] = family;
            int familyPosition = familyCounts.GetValueOrDefault(family) + 1;
            familyCounts[family] = familyPosition;
            baselineFamilyPositions[occurrenceId] = familyPosition;
        }

        int[] targetFamilyPositions = new int[baselineRules.Count];
        familyCounts.Clear();
        for (int targetIndex = 0; targetIndex < desiredOrder.Count; targetIndex++)
        {
            int occurrenceId = desiredOrder[targetIndex];
            FirewallAddressFamily family = families[occurrenceId];
            int familyPosition = familyCounts.GetValueOrDefault(family) + 1;
            familyCounts[family] = familyPosition;
            targetFamilyPositions[occurrenceId] = familyPosition;
        }

        return new RuleOrderingResultProjection(
            result.Operations.Select(operation => Project(operation, baselineFamilyPositions, targetFamilyPositions, desiredOrder)).ToArray(),
            result.PendingOperations.Select(move => Project(move, baselineFamilyPositions, targetFamilyPositions, desiredOrder)).ToArray(),
            result.BlockedOperations.Select(move => Project(move, baselineFamilyPositions, targetFamilyPositions, desiredOrder)).ToArray());
    }

    private static RuleOrderingOperationProjection Project(
        RuleReorderOperationResponse operation,
        IReadOnlyList<int> baselineFamilyPositions,
        IReadOnlyList<int> targetFamilyPositions,
        IReadOnlyList<int> desiredOrder)
    {
        RuleOrderingMoveProjection move = Project(operation.Move, baselineFamilyPositions, targetFamilyPositions, desiredOrder);
        return new RuleOrderingOperationProjection(operation, move.BaselineFamilyPosition, move.TargetFamilyPosition);
    }

    private static RuleOrderingMoveProjection Project(
        RuleReorderMoveResponse move,
        IReadOnlyList<int> baselineFamilyPositions,
        IReadOnlyList<int> targetFamilyPositions,
        IReadOnlyList<int> desiredOrder)
    {
        if (move.OccurrenceId < 0 || move.OccurrenceId >= baselineFamilyPositions.Count)
        {
            throw new InvalidOperationException("The reorder result references an occurrence outside the reviewed baseline.");
        }
        if (move.TargetIndex < 0 || move.TargetIndex >= desiredOrder.Count || desiredOrder[move.TargetIndex] != move.OccurrenceId)
        {
            throw new InvalidOperationException("The reorder result target does not match the reviewed desired order.");
        }

        return new RuleOrderingMoveProjection(
            move,
            baselineFamilyPositions[move.OccurrenceId],
            targetFamilyPositions[move.OccurrenceId]);
    }

    private static void ValidateDesiredOrder(IReadOnlyList<int> desiredOrder, int occurrenceCount)
    {
        if (desiredOrder.Count != occurrenceCount)
        {
            throw new ArgumentException("The desired ordering must contain every baseline occurrence exactly once.", nameof(desiredOrder));
        }

        bool[] seen = new bool[occurrenceCount];
        for (int index = 0; index < desiredOrder.Count; index++)
        {
            int occurrenceId = desiredOrder[index];
            if (occurrenceId < 0 || occurrenceId >= occurrenceCount || seen[occurrenceId])
            {
                throw new ArgumentException("The desired ordering must be a permutation of the baseline occurrences.", nameof(desiredOrder));
            }

            seen[occurrenceId] = true;
        }
    }
}
