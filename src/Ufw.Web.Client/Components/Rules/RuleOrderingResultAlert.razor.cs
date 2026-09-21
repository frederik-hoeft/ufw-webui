using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Ordering;

namespace Ufw.Web.Client.Components.Rules;

public sealed partial class RuleOrderingResultAlert
{
    [Inject]
    private IRuleOrderingResultProjectionService ResultProjectionService { get; set; } = null!;

    [Parameter, EditorRequired]
    public RuleReorderResponse Result { get; set; } = null!;

    [Parameter, EditorRequired]
    public IReadOnlyList<ListedFirewallRule> BaselineRules { get; set; } = [];

    [Parameter, EditorRequired]
    public IReadOnlyList<int> DesiredOrder { get; set; } = [];

    private RuleOrderingResultProjection Projection { get; set; } = RuleOrderingResultProjection.Empty;

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

    protected override void OnParametersSet() => Projection = ResultProjectionService.Create(Result, BaselineRules, DesiredOrder);

    private string DescribeOperation(RuleOrderingOperationProjection operation) =>
        RulesText[
            "OrderingOperationReport",
            operation.BaselineFamilyPosition,
            operation.TargetFamilyPosition,
            DescribeOperationOutcome(operation.Operation.Outcome)];

    private string DescribeMove(RuleOrderingMoveProjection move) =>
        RulesText["OrderingPendingMove", move.BaselineFamilyPosition, move.TargetFamilyPosition];

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
