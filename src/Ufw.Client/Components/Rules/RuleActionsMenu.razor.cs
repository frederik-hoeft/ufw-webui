using Microsoft.AspNetCore.Components;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

public sealed partial class RuleActionsMenu
{
    [Parameter, EditorRequired]
    public ListedFirewallRule Rule { get; set; } = null!;

    [Parameter]
    public bool OrderingDisabled { get; set; }

    [Parameter]
    public bool InsertionDisabled { get; set; }

    [Parameter]
    public bool MutationDisabled { get; set; }

    [Parameter]
    public bool CanOrder { get; set; }

    [Parameter]
    public bool CanMutate { get; set; }

    [Parameter]
    public EventCallback<ListedFirewallRule> MoveToPositionRequested { get; set; }

    [Parameter]
    public EventCallback<ListedFirewallRule> DeleteRequested { get; set; }

    [Parameter]
    public EventCallback<RuleInsertionActionRequest> InsertionRequested { get; set; }

    private string ActionsLabel => Rule.DisplayNumber is { } number
        ? RulesText["ActionsForRule", number.ToString(System.Globalization.CultureInfo.CurrentCulture)]
        : RulesText["RuleActions"];

    private Task RequestInsertionAsync(RuleInsertionPlacement placement)
        => InsertionRequested.InvokeAsync(new RuleInsertionActionRequest(Rule, placement));
}
