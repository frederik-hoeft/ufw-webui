using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Components.Rules;

public sealed partial class RuleOrderingResultAlert
{
    [Parameter, EditorRequired]
    public RuleReorderResponse Result { get; set; } = null!;

    private Severity AlertSeverity => Result.Outcome switch
    {
        RuleReorderOutcome.Completed => Severity.Success,
        RuleReorderOutcome.StaleBaseline or RuleReorderOutcome.PreconditionFailed => Severity.Warning,
        RuleReorderOutcome.PartiallyCompleted => Severity.Warning,
        RuleReorderOutcome.RecoveryFailed or RuleReorderOutcome.StateUncertain => Severity.Error,
        _ => Severity.Info,
    };

    private string ResultTitle => Result.Outcome switch
    {
        RuleReorderOutcome.Completed => RulesText["OrderingResultCompleted"],
        RuleReorderOutcome.StaleBaseline => RulesText["OrderingResultStale"],
        RuleReorderOutcome.PreconditionFailed => RulesText["OrderingResultPrecondition"],
        RuleReorderOutcome.PartiallyCompleted => RulesText["OrderingResultPartial"],
        RuleReorderOutcome.RecoveryFailed => RulesText["OrderingResultRecoveryFailed"],
        RuleReorderOutcome.StateUncertain => RulesText["OrderingResultUncertain"],
        _ => RulesText["OrderingResult"],
    };

    private string DescribeOperation(RuleReorderOperationResponse operation) =>
        RulesText[
            "OrderingOperationReport",
            operation.Move.OccurrenceId + 1,
            operation.Move.TargetIndex + 1,
            DescribeOperationOutcome(operation.Outcome)];

    private string DescribeMove(RuleReorderMoveResponse move) =>
        RulesText["OrderingPendingMove", move.OccurrenceId + 1, move.TargetIndex + 1];

    private string DescribeOperationOutcome(RuleReorderOperationOutcome outcome) => outcome switch
    {
        RuleReorderOperationOutcome.Applied => RulesText["OrderingOperationApplied"],
        RuleReorderOperationOutcome.AppliedAfterProcessFailure => RulesText["OrderingOperationAppliedAfterFailure"],
        RuleReorderOperationOutcome.FailedAndRestored => RulesText["OrderingOperationRestored"],
        RuleReorderOperationOutcome.PresenceConfirmedAfterInterruption => RulesText["OrderingOperationPresenceConfirmed"],
        RuleReorderOperationOutcome.RecoveryFailed => RulesText["OrderingOperationRecoveryFailed"],
        _ => outcome.ToString(),
    };
}
