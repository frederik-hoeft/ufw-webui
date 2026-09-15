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
    private RuleRowProjection? _dragTargetRow;
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
        row.Rule.DisplayNumber is { } number
            ? RulesText["DragRuleNumber", number.ToString(System.Globalization.CultureInfo.CurrentCulture)]
            : RulesText["DragRule"];

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

        if (ReferenceEquals(_dragTargetRow, row) && !ReferenceEquals(_draggedRow, row))
        {
            classes.Add("rule-row-drop-target");
        }

        return string.Join(' ', classes);
    }

    private string ReadOnlyRowClass(RuleRowProjection row) =>
        ReferenceEquals(_dragTargetRow, row) && !ReferenceEquals(_draggedRow, row)
            ? "rule-row-drop-target"
            : string.Empty;

    private string ReadOnlyMobileCardClass(RuleRowProjection row) =>
        ReferenceEquals(_dragTargetRow, row) && !ReferenceEquals(_draggedRow, row)
            ? "rule-mobile-card rule-mobile-card-readonly rule-row-drop-target"
            : "rule-mobile-card rule-mobile-card-readonly";

    private string MobileCardClass(RuleRowProjection row)
    {
        List<string> classes = ["rule-mobile-card"];
        if (row.PositionChange is { DirectlyMoved: true })
        {
            classes.Add("rule-mobile-card-ordering-direct");
        }

        if (ReferenceEquals(_dragTargetRow, row) && !ReferenceEquals(_draggedRow, row))
        {
            classes.Add("rule-mobile-card-drop-target");
        }

        return string.Join(' ', classes);
    }

    private string DragEnabled(RuleRowProjection row) =>
        !OrderingDisabled && row.CanOrder ? "true" : "false";

    private void BeginDrag(RuleRowProjection row)
    {
        if (!OrderingDisabled && row.CanOrder)
        {
            _draggedRow = row;
            _dragTargetRow = row;
        }
    }

    private void SetDragTarget(RuleRowProjection row)
    {
        if (_draggedRow is null || ReferenceEquals(_dragTargetRow, row))
        {
            return;
        }

        if (_draggedRow.AddressFamily != row.AddressFamily)
        {
            if (_dragTargetRow is not null)
            {
                _dragTargetRow = null;
                StateHasChanged();
            }
            return;
        }

        _dragTargetRow = row;
        StateHasChanged();
    }

    private void EndDrag()
    {
        _draggedRow = null;
        _dragTargetRow = null;
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
