using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Rules;
using Ufw.Client.Rules.Filtering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

public sealed partial class RuleDesktopRow
{
    [Parameter, EditorRequired]
    public RuleRowProjection Row { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<RuleMatchEvidence> MatchEvidence { get; set; } = [];

    [Parameter]
    public bool OrderingDisabled { get; set; }

    [Parameter]
    public bool InsertionDisabled { get; set; }

    [Parameter]
    public bool MutationDisabled { get; set; }

    [Parameter]
    public RuleDropIndicatorEdge? DropIndicatorEdge { get; set; }

    [Parameter, EditorRequired]
    public Action<RuleRowProjection> DragStarted { get; set; } = null!;

    [Parameter, EditorRequired]
    public Action<RuleRowProjection> DragEntered { get; set; } = null!;

    [Parameter, EditorRequired]
    public Action DragEnded { get; set; } = null!;

    [Parameter, EditorRequired]
    public Func<RuleRowProjection, Task> DropRequested { get; set; } = null!;

    [Parameter]
    public EventCallback<RuleRowProjection> MoveToPositionRequested { get; set; }

    [Parameter]
    public EventCallback<ListedFirewallRule> DeleteRequested { get; set; }

    [Parameter]
    public EventCallback<RuleInsertionActionRequest> InsertionRequested { get; set; }

    // Native dragenter may bubble repeatedly while crossing descendants of the same row. Keep the callback non-rendering;
    // the workspace schedules a render only when the effective drop target actually changes.
    private Action DragEnterHandler => EventUtil.AsNonRenderingEventHandler(this, () => DragEntered(Row));

    private Action BeginDragHandler => EventUtil.AsNonRenderingEventHandler(this, () => DragStarted(Row));

    private Action EndDragHandler => EventUtil.AsNonRenderingEventHandler(this, DragEnded);

    private string DragHandleLabel =>
        RulesText["DragRulePosition", Row.FamilyPosition.ToString(System.Globalization.CultureInfo.CurrentCulture)];

    private string DragHandleTitle => !Row.CanOrder ? RulesText["CannotOrderReadOnly"] : RulesText["DragRealTitle"];

    private string DragHandleClass =>
        OrderingDisabled || !Row.CanOrder
            ? "rule-drag-handle rule-drag-handle-disabled"
            : "rule-drag-handle";

    private string DragEnabled => !OrderingDisabled && Row.CanOrder ? "true" : "false";

    private string RowClass
    {
        get
        {
            List<string> classes = ["rule-desktop-row"];
            if (Row.PositionChange is { DirectlyMoved: true })
            {
                classes.Add("ordering-direct");
            }
            if (MatchEvidence.Count > 0)
            {
                classes.Add("has-match-context");
            }

            if (DropIndicatorEdge is { } edge)
            {
                classes.Add(edge == RuleDropIndicatorEdge.Before ? "drop-before" : "drop-after");
            }

            return string.Join(' ', classes);
        }
    }

    private string ReadOnlyRowClass
    {
        get
        {
            List<string> classes = ["rule-desktop-row"];
            if (MatchEvidence.Count > 0)
            {
                classes.Add("has-match-context");
            }
            if (DropIndicatorEdge is { } edge)
            {
                classes.Add(edge == RuleDropIndicatorEdge.Before ? "drop-before" : "drop-after");
            }
            return string.Join(' ', classes);
        }
    }

    private Task DropAsync() => DropRequested(Row);

    private static string PositionChangeClass(bool directlyMoved) =>
        directlyMoved
            ? "rule-position-change rule-position-change-direct"
            : "rule-position-change rule-position-change-indirect";

    private static string ActionClass(FirewallAction action) => action switch
    {
        FirewallAction.Allow => "rule-action rule-action-allow",
        FirewallAction.Deny => "rule-action rule-action-deny",
        FirewallAction.Reject => "rule-action rule-action-reject",
        FirewallAction.Limit => "rule-action rule-action-limit",
        _ => "rule-action",
    };

    private string PositionChangeLabel(int originalPosition, int currentPosition, bool directlyMoved) =>
        directlyMoved
            ? RulesText["DirectMovePositionAria", originalPosition, currentPosition]
            : RulesText["IndirectShiftPositionAria", originalPosition, currentPosition];
}
