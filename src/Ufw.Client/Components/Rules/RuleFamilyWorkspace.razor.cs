using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Api;
using Ufw.Client.Rules;
using Ufw.Client.Rules.Filtering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

public sealed partial class RuleFamilyWorkspace
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

    [Parameter, EditorRequired]
    public required RuleFamilyProjection Family { get; set; }

    [Parameter, EditorRequired]
    public required RuleQuery Query { get; set; }

    [Parameter]
    public IReadOnlyList<RuleQueryRow>? QueryRows { get; set; }

    [Parameter]
    public bool QueryActive { get; set; }

    [Parameter]
    public bool QueryChangesDisabled { get; set; }

    [Parameter]
    public EventCallback<RuleQuery> QueryChanged { get; set; }

    [Parameter]
    public bool Loading { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool OrderingDisabled { get; set; }

    [Parameter]
    public bool InsertionDisabled { get; set; }

    [Parameter]
    public bool MetadataEditDisabled { get; set; }

    [Parameter]
    public EventCallback<RuleRowProjection> MetadataEditRequested { get; set; }

    [Parameter]
    public EventCallback<ListedFirewallRule> DeleteRequested { get; set; }

    [Parameter]
    public EventCallback<RuleInsertionActionRequest> InsertionRequested { get; set; }

    [Parameter]
    public EventCallback<RuleMoveRequest> MoveRequested { get; set; }

    private IReadOnlyList<RuleQueryRow> DisplayedRows => QueryRows ?? Family.Rows.Select(static row => new RuleQueryRow(row, [])).ToArray();

    private RuleDropIndicatorEdge? DropIndicatorEdge(RuleRowProjection row) =>
        ReferenceEquals(_dropTarget?.Row, row) ? _dropTarget.IndicatorEdge : null;

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

        if (ReferenceEquals(source, row))
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
        bool hadDropTarget = _dropTarget is not null;
        _draggedRow = null;
        _dropTarget = null;
        if (hadDropTarget)
        {
            StateHasChanged();
        }
    }

    private async Task DropAsync(RuleRowProjection target)
    {
        RuleRowProjection? source = _draggedRow;
        try
        {
            if (source is null
                || ReferenceEquals(source, target)
                || OrderingDisabled
                || !source.CanOrder)
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
}
