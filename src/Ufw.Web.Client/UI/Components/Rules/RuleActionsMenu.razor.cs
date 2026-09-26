using Microsoft.AspNetCore.Components;
using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.UI.Components.Rules;

public sealed partial class RuleActionsMenu
{
    [Parameter, EditorRequired]
    public ListedFirewallRule Rule { get; set; } = null!;

    [Parameter, EditorRequired]
    public int FamilyPosition { get; set; }

    [Parameter]
    public bool OrderingDisabled { get; set; }

    [Parameter]
    public bool InsertionDisabled { get; set; }

    [Parameter]
    public bool MutationDisabled { get; set; }

    [Parameter]
    public bool Compact { get; set; }

    [Parameter]
    public bool MetadataEditDisabled { get; set; }

    [Parameter]
    public bool CanOrder { get; set; }

    [Parameter]
    public bool CanMutate { get; set; }

    [Parameter]
    public EventCallback MetadataEditRequested { get; set; }

    [Parameter]
    public EventCallback MoveToPositionRequested { get; set; }

    [Parameter]
    public EventCallback<ListedFirewallRule> DeleteRequested { get; set; }

    [Parameter]
    public EventCallback<RuleInsertionActionRequest> InsertionRequested { get; set; }

    private string MenuClass => Compact ? "rule-actions-menu rule-actions-menu-compact" : "rule-actions-menu";

    private string ActionsLabel =>
        RulesText["ActionsForRulePosition", FamilyPosition.ToString(System.Globalization.CultureInfo.CurrentCulture)];

    private Task RequestInsertionAsync(RuleInsertionPlacement placement)
        => InsertionRequested.InvokeAsync(new RuleInsertionActionRequest(Rule, placement));
}
