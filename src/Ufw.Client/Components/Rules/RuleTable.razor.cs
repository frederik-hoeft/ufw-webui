using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Api;
using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

public sealed partial class RuleTable
{
    private static readonly DialogOptions s_moveDialogOptions = new()
    {
        BackdropClick = false,
        CloseButton = true,
        CloseOnEscapeKey = true,
        DefaultFocus = DefaultFocus.FirstChild,
        FullWidth = true,
        MaxWidth = MaxWidth.ExtraSmall,
    };

    private RuleRowProjection? _draggedRow;
    private RuleDropTargetProjection? _dropTarget;
    private RuleTableProjection _projection = RuleTableProjection.Empty;

    [Parameter]
    public IReadOnlyList<ListedFirewallRule> Rules { get; set; } = [];

    [Parameter]
    public RuleOrderingPreview? OrderingPreview { get; set; }

    [Parameter]
    public bool Loading { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool OrderingDisabled { get; set; }

    [Parameter]
    public bool InsertionDisabled { get; set; }

    [Parameter]
    public EventCallback<ListedFirewallRule> DeleteRequested { get; set; }

    [Parameter]
    public EventCallback<RuleInsertionActionRequest> InsertionRequested { get; set; }

    [Parameter]
    public EventCallback<RuleMoveRequest> MoveRequested { get; set; }

    protected override void OnParametersSet() =>
        _projection = ProjectionService.Create(Rules, OrderingPreview);

    // Keep dragover browser-only in the Razor markup. It fires continuously during a native drag, and a Blazor event handler
    // would otherwise schedule a full component render for every event. Dragenter only renders when the target row changes.
    private Action BeginDragHandler(RuleRowProjection row) =>
        EventUtil.AsNonRenderingEventHandler(this, () => BeginDrag(row));

    private Action DragEnterHandler(RuleRowProjection row) =>
        EventUtil.AsNonRenderingEventHandler(this, () => SetDragTarget(row));

    private string DragHandleLabel(RuleRowProjection row) =>
        RulesText["DragRulePosition", row.FamilyPosition.ToString(System.Globalization.CultureInfo.CurrentCulture)];

    private string DragHandleTitle(RuleRowProjection row)
    {
        if (!row.CanOrder)
        {
            return RulesText["CannotOrderReadOnly"];
        }

        return RulesText["DragRealTitle"];
    }

    private string DragHandleClass(RuleRowProjection row) =>
        OrderingDisabled || !row.CanOrder
            ? "rule-drag-handle rule-drag-handle-disabled"
            : "rule-drag-handle";

    private static string FamilyHeadingId(FirewallAddressFamily family) =>
        family == FirewallAddressFamily.IPv6 ? "firewall-ipv6-rules-heading" : "firewall-ipv4-rules-heading";

    private string DescribeFamilyRuleCount(int count) => count == 1
        ? RulesText["RuleCountOne"]
        : RulesText["RuleCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

    private string RowClass(RuleRowProjection row)
    {
        List<string> classes = [];
        if (row.PositionChange is { DirectlyMoved: true })
        {
            classes.Add("rule-row-ordering-direct");
        }

        if (DropIndicatorEdge(row) is { } edge)
        {
            classes.Add(edge == RuleDropIndicatorEdge.Before ? "rule-row-drop-target-before" : "rule-row-drop-target-after");
        }

        return string.Join(' ', classes);
    }

    private string ReadOnlyRowClass(RuleRowProjection row) => DropIndicatorEdge(row) switch
    {
        RuleDropIndicatorEdge.Before => "rule-row-drop-target-before",
        RuleDropIndicatorEdge.After => "rule-row-drop-target-after",
        _ => string.Empty,
    };

    private string ReadOnlyMobileCardClass(RuleRowProjection row) => DropIndicatorEdge(row) switch
    {
        RuleDropIndicatorEdge.Before => "rule-mobile-card rule-mobile-card-readonly rule-mobile-card-drop-target-before",
        RuleDropIndicatorEdge.After => "rule-mobile-card rule-mobile-card-readonly rule-mobile-card-drop-target-after",
        _ => "rule-mobile-card rule-mobile-card-readonly",
    };

    private string MobileCardClass(RuleRowProjection row)
    {
        List<string> classes = ["rule-mobile-card"];
        if (row.PositionChange is { DirectlyMoved: true })
        {
            classes.Add("rule-mobile-card-ordering-direct");
        }

        if (DropIndicatorEdge(row) is { } edge)
        {
            classes.Add(edge == RuleDropIndicatorEdge.Before ? "rule-mobile-card-drop-target-before" : "rule-mobile-card-drop-target-after");
        }

        return string.Join(' ', classes);
    }

    private RuleDropIndicatorEdge? DropIndicatorEdge(RuleRowProjection row) =>
        ReferenceEquals(_dropTarget?.Row, row) ? _dropTarget.IndicatorEdge : null;

    private string DragEnabled(RuleRowProjection row) =>
        !OrderingDisabled && row.CanOrder ? "true" : "false";

    private void BeginDrag(RuleRowProjection row)
    {
        if (!OrderingDisabled && row.CanOrder)
        {
            _draggedRow = row;
            _dropTarget = null;
        }
    }

    private void SetDragTarget(RuleRowProjection row)
    {
        RuleRowProjection? source = _draggedRow;
        if (source is null || ReferenceEquals(_dropTarget?.Row, row))
        {
            return;
        }

        if (ReferenceEquals(source, row) || source.AddressFamily != row.AddressFamily)
        {
            if (_dropTarget is not null)
            {
                _dropTarget = null;
                StateHasChanged();
            }
            return;
        }

        _dropTarget = new RuleDropTargetProjection(source, row);
        StateHasChanged();
    }

    private void EndDrag()
    {
        _draggedRow = null;
        _dropTarget = null;
    }

    private async Task DropAsync(RuleRowProjection target)
    {
        RuleRowProjection? source = _draggedRow;
        try
        {
            if (source is null
                || ReferenceEquals(source, target)
                || OrderingDisabled
                || !source.CanOrder
                || source.AddressFamily != target.AddressFamily)
            {
                return;
            }

            if (source.OccurrenceId >= 0 && target.FamilyPosition > 0)
            {
                await MoveRequested.InvokeAsync(new RuleMoveRequest(source.OccurrenceId, source.AddressFamily, target.FamilyPosition));
            }
        }
        finally
        {
            EndDrag();
        }
    }

    private async Task RequestMoveToPositionAsync(RuleRowProjection row)
    {
        if (OrderingDisabled || !row.CanOrder || row.FamilyPosition < 1)
        {
            return;
        }

        DialogParameters<MoveRuleDialog> parameters = [];
        parameters.Add(component => component.CurrentPosition, row.FamilyPosition);
        parameters.Add(component => component.RuleCount, row.FamilyCount);
        parameters.Add(component => component.AddressFamily, row.AddressFamily);

        IDialogReference dialog = await DialogService.ShowAsync<MoveRuleDialog>(RulesText["MoveDialogTitle"], parameters, s_moveDialogOptions);
        int? targetPosition = await dialog.GetReturnValueAsync<int?>();
        if (targetPosition is not null && targetPosition.Value != row.FamilyPosition && row.OccurrenceId >= 0)
        {
            await MoveRequested.InvokeAsync(new RuleMoveRequest(row.OccurrenceId, row.AddressFamily, targetPosition.Value));
        }
    }

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
