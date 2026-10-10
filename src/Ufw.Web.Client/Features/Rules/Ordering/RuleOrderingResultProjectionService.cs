using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Features.Rules.Ordering;

internal sealed class RuleOrderingResultProjectionService : IRuleOrderingResultProjectionService
{
    public RuleOrderingResultProjection Create(RuleReorderResponse result, IReadOnlyList<ListedFirewallRule> baselineRules, IReadOnlyList<int> desiredOrder)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(baselineRules);
        ArgumentNullException.ThrowIfNull(desiredOrder);
        RuleOrderPermutation permutation = RuleOrderPermutation.Create(desiredOrder, baselineRules.Count);
        IReadOnlyList<int> validatedOrder = permutation.Occurrences;

        RuleSnapshotIndex index = new(baselineRules);
        int[] baselineFamilyPositions = [.. Enumerable.Range(0, index.Count).Select(index.GetFamilyPosition)];
        Dictionary<FirewallAddressFamily, int> familyCounts = [];
        int[] targetFamilyPositions = new int[baselineRules.Count];
        for (int targetIndex = 0; targetIndex < validatedOrder.Count; targetIndex++)
        {
            int occurrenceId = validatedOrder[targetIndex];
            FirewallAddressFamily family = index.GetFamily(occurrenceId);
            int familyPosition = familyCounts.GetValueOrDefault(family) + 1;
            familyCounts[family] = familyPosition;
            targetFamilyPositions[occurrenceId] = familyPosition;
        }

        return new RuleOrderingResultProjection(
            result.Operations.Select(operation => Project(operation, baselineFamilyPositions, targetFamilyPositions, validatedOrder)).ToArray(),
            result.PendingOperations.Select(move => Project(move, baselineFamilyPositions, targetFamilyPositions, validatedOrder)).ToArray(),
            result.BlockedOperations.Select(move => Project(move, baselineFamilyPositions, targetFamilyPositions, validatedOrder)).ToArray());
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

        return new RuleOrderingMoveProjection(move, baselineFamilyPositions[move.OccurrenceId], targetFamilyPositions[move.OccurrenceId]);
    }
}
