using Microsoft.AspNetCore.Components;
using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.UI.Components.Rules;

public sealed partial class RuleActionsMenu
{
    [Parameter, EditorRequired]
    public ListedFirewallRule Rule { get; set; } = null!;

    [Parameter, EditorRequired]
    public int OccurrenceId { get; set; }

    [Parameter, EditorRequired]
    public int FamilyPosition { get; set; }

    [Parameter]
    public bool OrderingDisabled { get; set; }

    [Parameter]
    public bool InsertionDisabled { get; set; }

    [Parameter]
    public bool MutationDisabled { get; set; }

    [Parameter]
    public bool MetadataEditDisabled { get; set; }

    [Parameter]
    public bool TemplateSaveDisabled { get; set; }

    [Parameter]
    public bool CanOrder { get; set; }

    [Parameter]
    public bool CanMutate { get; set; }

    [Parameter]
    public bool CanEdit { get; set; }

    [Parameter]
    public bool CanSaveAsTemplate { get; set; }

    [Parameter]
    public EventCallback EditRequested { get; set; }

    [Parameter]
    public EventCallback MetadataEditRequested { get; set; }

    [Parameter]
    public EventCallback SaveAsTemplateRequested { get; set; }

    [Parameter]
    public EventCallback DisableRequested { get; set; }

    [Parameter]
    public EventCallback MoveToPositionRequested { get; set; }

    [Parameter]
    public EventCallback<ListedFirewallRule> DeleteRequested { get; set; }

    [Parameter]
    public EventCallback<RuleInsertionActionRequest> InsertionRequested { get; set; }

    private string ActionsLabel =>
        RulesText["ActionsForRulePosition", FamilyPosition.ToString(System.Globalization.CultureInfo.CurrentCulture)];

    private Task RequestInsertionAsync(RuleInsertionPlacement placement)
        => InsertionRequested.InvokeAsync(new RuleInsertionActionRequest(OccurrenceId, placement));
}
