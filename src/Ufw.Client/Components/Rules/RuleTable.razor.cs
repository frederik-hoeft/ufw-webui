using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Client.Api;
using Ufw.Client.RuleOrdering;
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

    private ListedFirewallRule? _draggedRule;
    private ListedFirewallRule? _dragTargetRule;

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

    private string DragHandleLabel(ListedFirewallRule rule)
        => rule.DisplayNumber is { } number
            ? RulesText["DragRuleNumber", number.ToString(System.Globalization.CultureInfo.CurrentCulture)]
            : RulesText["DragRule"];

    private string DragHandleTitle(ListedFirewallRule rule)
    {
        if (!CanOrderRule(rule))
        {
            return RulesText["CannotOrderReadOnly"];
        }

        return RulesText["DragRealTitle"];
    }

    private string DragHandleClass(ListedFirewallRule rule)
    {
        List<string> classes = ["rule-drag-handle"];
        if (OrderingDisabled || !CanOrderRule(rule))
        {
            classes.Add("rule-drag-handle-disabled");
        }

        return string.Join(' ', classes);
    }

    private string RowClass(ListedFirewallRule rule)
    {
        List<string> classes = [];
        if (TryGetPositionChange(rule, out _, out _, out bool directlyMoved))
        {
            classes.Add(directlyMoved ? "rule-row-ordering-direct" : "rule-row-ordering-indirect");
        }

        if (ReferenceEquals(_dragTargetRule, rule) && !ReferenceEquals(_draggedRule, rule))
        {
            classes.Add("rule-row-drop-target");
        }

        return string.Join(' ', classes);
    }

    private string ReadOnlyRowClass(ListedFirewallRule rule)
        => ReferenceEquals(_dragTargetRule, rule) && !ReferenceEquals(_draggedRule, rule)
            ? "rule-row-readonly rule-row-drop-target"
            : "rule-row-readonly";

    private string ReadOnlyMobileCardClass(ListedFirewallRule rule)
        => ReferenceEquals(_dragTargetRule, rule) && !ReferenceEquals(_draggedRule, rule)
            ? "rule-mobile-card rule-mobile-card-readonly rule-row-drop-target"
            : "rule-mobile-card rule-mobile-card-readonly";

    private string MobileCardClass(ListedFirewallRule rule)
    {
        List<string> classes = ["rule-mobile-card"];
        if (TryGetPositionChange(rule, out _, out _, out bool directlyMoved) && directlyMoved)
        {
            classes.Add("rule-mobile-card-ordering-direct");
        }

        if (ReferenceEquals(_dragTargetRule, rule) && !ReferenceEquals(_draggedRule, rule))
        {
            classes.Add("rule-mobile-card-drop-target");
        }

        return string.Join(' ', classes);
    }

    private string DragEnabled(ListedFirewallRule rule)
        => !OrderingDisabled && CanOrderRule(rule) ? "true" : "false";

    internal static bool CanOrderRule(ListedFirewallRule rule) => rule.Parsed && rule.Rule is not null;

    private bool CanMutateRule(ListedFirewallRule rule)
    {
        if (!rule.Parsed || rule.Rule is null || string.IsNullOrWhiteSpace(rule.RuleId))
        {
            return false;
        }

        int matches = 0;
        foreach (ListedFirewallRule candidate in Rules)
        {
            if (string.Equals(candidate.RuleId, rule.RuleId, StringComparison.Ordinal) && ++matches > 1)
            {
                return false;
            }
        }

        return matches == 1;
    }

    private void BeginDrag(ListedFirewallRule rule)
    {
        if (!OrderingDisabled && CanOrderRule(rule))
        {
            _draggedRule = rule;
            _dragTargetRule = rule;
        }
    }

    private void SetDragTarget(ListedFirewallRule rule)
    {
        if (_draggedRule is not null)
        {
            _dragTargetRule = rule;
        }
    }

    private void EndDrag()
    {
        _draggedRule = null;
        _dragTargetRule = null;
    }

    private async Task DropAsync(ListedFirewallRule target)
    {
        ListedFirewallRule? source = _draggedRule;
        try
        {
            if (source is null
                || ReferenceEquals(source, target)
                || OrderingDisabled
                || !CanOrderRule(source))
            {
                return;
            }

            int sourcePosition = PositionOf(source);
            int targetPosition = PositionOf(target);
            if (sourcePosition > 0 && targetPosition > 0)
            {
                int occurrenceId = OrderingPreview?.GetOccurrenceId(source) ?? sourcePosition - 1;
                await MoveRequested.InvokeAsync(new RuleMoveRequest(occurrenceId, targetPosition));
            }
        }
        finally
        {
            EndDrag();
        }
    }

    private async Task RequestMoveToPositionAsync(ListedFirewallRule rule)
    {
        if (OrderingDisabled || !CanOrderRule(rule))
        {
            return;
        }

        int currentPosition = PositionOf(rule);
        if (currentPosition < 1)
        {
            return;
        }

        DialogParameters<MoveRuleDialog> parameters = [];
        parameters.Add(component => component.CurrentPosition, currentPosition);
        parameters.Add(component => component.RuleCount, Rules.Count);

        IDialogReference dialog = await DialogService.ShowAsync<MoveRuleDialog>(RulesText["MoveDialogTitle"], parameters, s_moveDialogOptions);
        int? targetPosition = await dialog.GetReturnValueAsync<int?>();
        if (targetPosition is not null && targetPosition.Value != currentPosition)
        {
            int occurrenceId = OrderingPreview?.GetOccurrenceId(rule) ?? currentPosition - 1;
            await MoveRequested.InvokeAsync(new RuleMoveRequest(occurrenceId, targetPosition.Value));
        }
    }

    private bool TryGetPositionChange(ListedFirewallRule rule, out int originalPosition, out int currentPosition, out bool directlyMoved)
    {
        originalPosition = 0;
        currentPosition = 0;
        directlyMoved = false;

        int? original = OrderingPreview?.GetOriginalPosition(rule);
        if (original is null || rule.DisplayNumber is null || original.Value == rule.DisplayNumber.Value)
        {
            return false;
        }

        originalPosition = original.Value;
        currentPosition = rule.DisplayNumber.Value;
        directlyMoved = OrderingPreview!.WasDirectlyMoved(rule);
        return true;
    }

    private static string PositionChangeClass(bool directlyMoved)
        => directlyMoved
            ? "rule-position-change rule-position-change-direct"
            : "rule-position-change rule-position-change-indirect";

    private string PositionChangeLabel(int originalPosition, int currentPosition, bool directlyMoved)
        => directlyMoved
            ? RulesText["DirectMovePositionAria", originalPosition, currentPosition]
            : RulesText["IndirectShiftPositionAria", originalPosition, currentPosition];

    private int PositionOf(ListedFirewallRule rule)
    {
        for (int index = 0; index < Rules.Count; index++)
        {
            if (ReferenceEquals(Rules[index], rule))
            {
                return index + 1;
            }
        }

        return 0;
    }
}
