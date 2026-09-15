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
    private HashSet<string> _uniqueRuleIds = new(StringComparer.Ordinal);
    private IReadOnlyList<RuleFamilyGroup> _ruleGroups = [];

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

    protected override void OnParametersSet()
    {
        Dictionary<string, int> ruleIdCounts = new(StringComparer.Ordinal);
        List<ListedFirewallRule> ipv4Rules = [];
        List<ListedFirewallRule> ipv6Rules = [];
        foreach (ListedFirewallRule rule in Rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.RuleId))
            {
                ruleIdCounts[rule.RuleId] = ruleIdCounts.GetValueOrDefault(rule.RuleId) + 1;
            }

            if (ListedFirewallRuleFamily.GetObservedFamily(rule) == FirewallAddressFamily.IPv6)
            {
                ipv6Rules.Add(rule);
            }
            else
            {
                ipv4Rules.Add(rule);
            }
        }

        _uniqueRuleIds = [.. ruleIdCounts.Where(static pair => pair.Value == 1).Select(static pair => pair.Key)];
        List<RuleFamilyGroup> groups = [];
        if (ipv4Rules.Count > 0)
        {
            groups.Add(new RuleFamilyGroup(FirewallAddressFamily.IPv4, ipv4Rules));
        }
        if (ipv6Rules.Count > 0)
        {
            groups.Add(new RuleFamilyGroup(FirewallAddressFamily.IPv6, ipv6Rules));
        }
        _ruleGroups = groups;
    }

    // Keep dragover browser-only in the Razor markup. It fires continuously during a native drag, and a Blazor event handler
    // would otherwise schedule a full component render for every event. Dragenter only renders when the target row changes.
    private Action BeginDragHandler(ListedFirewallRule rule)
        => EventUtil.AsNonRenderingEventHandler(this, () => BeginDrag(rule));

    private Action DragEnterHandler(ListedFirewallRule rule)
        => EventUtil.AsNonRenderingEventHandler(this, () => SetDragTarget(rule));

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
        => OrderingDisabled || !CanOrderRule(rule)
            ? "rule-drag-handle rule-drag-handle-disabled"
            : "rule-drag-handle";

    private static string FamilyHeadingId(FirewallAddressFamily family)
        => family == FirewallAddressFamily.IPv6 ? "firewall-ipv6-rules-heading" : "firewall-ipv4-rules-heading";

    private string DescribeFamilyRuleCount(int count) => count == 1
        ? RulesText["RuleCountOne"]
        : RulesText["RuleCountMany", count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)];

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
        => rule.Parsed
            && rule.Rule is not null
            && rule.RuleId is { } ruleId
            && _uniqueRuleIds.Contains(ruleId);

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
        if (_draggedRule is null || ReferenceEquals(_dragTargetRule, rule))
        {
            return;
        }

        if (ListedFirewallRuleFamily.GetObservedFamily(_draggedRule) != ListedFirewallRuleFamily.GetObservedFamily(rule))
        {
            if (_dragTargetRule is not null)
            {
                _dragTargetRule = null;
                StateHasChanged();
            }
            return;
        }

        _dragTargetRule = rule;
        StateHasChanged();
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
                || !CanOrderRule(source)
                || ListedFirewallRuleFamily.GetObservedFamily(source) != ListedFirewallRuleFamily.GetObservedFamily(target))
            {
                return;
            }

            int occurrenceId = OrderingPreview?.GetOccurrenceId(source) ?? OccurrenceIdOf(source);
            int targetFamilyPosition = FamilyPositionOf(target);
            if (occurrenceId >= 0 && targetFamilyPosition > 0)
            {
                FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(source);
                await MoveRequested.InvokeAsync(new RuleMoveRequest(occurrenceId, family, targetFamilyPosition));
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

        FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rule);
        int currentPosition = FamilyPositionOf(rule);
        if (currentPosition < 1)
        {
            return;
        }

        DialogParameters<MoveRuleDialog> parameters = [];
        parameters.Add(component => component.CurrentPosition, currentPosition);
        parameters.Add(component => component.RuleCount, FamilyCount(family));
        parameters.Add(component => component.AddressFamily, family);

        IDialogReference dialog = await DialogService.ShowAsync<MoveRuleDialog>(RulesText["MoveDialogTitle"], parameters, s_moveDialogOptions);
        int? targetPosition = await dialog.GetReturnValueAsync<int?>();
        if (targetPosition is not null && targetPosition.Value != currentPosition)
        {
            int occurrenceId = OrderingPreview?.GetOccurrenceId(rule) ?? OccurrenceIdOf(rule);
            if (occurrenceId >= 0)
            {
                await MoveRequested.InvokeAsync(new RuleMoveRequest(occurrenceId, family, targetPosition.Value));
            }
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

    private int OccurrenceIdOf(ListedFirewallRule rule)
    {
        for (int index = 0; index < Rules.Count; index++)
        {
            if (ReferenceEquals(Rules[index], rule))
            {
                return index;
            }
        }

        return -1;
    }

    private int FamilyPositionOf(ListedFirewallRule rule)
    {
        FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rule);
        int position = 0;
        foreach (ListedFirewallRule candidate in Rules)
        {
            if (ListedFirewallRuleFamily.GetObservedFamily(candidate) != family)
            {
                continue;
            }

            position++;
            if (ReferenceEquals(candidate, rule))
            {
                return position;
            }
        }
        return 0;
    }

    private int FamilyCount(FirewallAddressFamily family)
        => Rules.Count(rule => ListedFirewallRuleFamily.GetObservedFamily(rule) == family);

    private sealed record RuleFamilyGroup(FirewallAddressFamily AddressFamily, IReadOnlyList<ListedFirewallRule> Rules);
}
