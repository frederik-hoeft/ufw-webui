using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules.Ordering;

internal sealed record RuleOrderingResultProjection(
    IReadOnlyList<RuleOrderingOperationProjection> Operations,
    IReadOnlyList<RuleOrderingMoveProjection> PendingOperations,
    IReadOnlyList<RuleOrderingMoveProjection> BlockedOperations)
{
    public static RuleOrderingResultProjection Empty { get; } = new([], [], []);
}

internal sealed record RuleOrderingOperationProjection(RuleReorderOperationResponse Operation, int BaselineFamilyPosition, int TargetFamilyPosition);

internal sealed record RuleOrderingMoveProjection(RuleReorderMoveResponse Move, int BaselineFamilyPosition, int TargetFamilyPosition);
