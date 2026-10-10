using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Rules;

public sealed partial class RuleDesktopRow
{
    private readonly RuleRowInteractionState _interaction = new();

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

    [Parameter, EditorRequired]
    public RuleRowActionHandlers Actions { get; set; } = null!;

    [Parameter]
    public bool MetadataEditDisabled { get; set; }

    [Parameter]
    public bool TemplateSaveDisabled { get; set; }

    [Parameter]
    public EventCallback<KnownHostInventoryResponse> KnownHostsChanged { get; set; }

    // Native dragenter may bubble repeatedly while crossing descendants of the same row. Keep the callback non-rendering;
    // the workspace schedules a render only when the effective drop target actually changes.
    private Action DragEnterHandler => EventUtil.AsNonRenderingEventHandler(this, () => DragEntered(Row));

    private string GroupClass => _interaction.MetadataExpanded
        ? "rule-row-group metadata-expanded"
        : "rule-row-group";

    private string RowClass
    {
        get
        {
            List<string> classes = ["rule-desktop-row"];
            if (MatchEvidence.Count > 0 || _interaction.MetadataExpanded)
            {
                classes.Add("has-expanded-content");
            }
            if (DetailsAvailable)
            {
                classes.Add("rule-details-available");
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
            if (MatchEvidence.Count > 0 || _interaction.MetadataExpanded)
            {
                classes.Add("has-expanded-content");
            }
            if (DropIndicatorEdge is { } edge)
            {
                classes.Add(edge == RuleDropIndicatorEdge.Before ? "drop-before" : "drop-after");
            }
            return string.Join(' ', classes);
        }
    }

    private bool DetailsAvailable => RuleRowInteractionState.DetailsAvailable(Row);

    private string CollapseMetadataLabel => RulesText["HideRuleMetadata", Row.FamilyPosition];

    private void ToggleMetadata() => _interaction.ToggleMetadata(Row);

    private void HandleKeyDown(KeyboardEventArgs args) => _interaction.HandleKeyDown(Row, args.Key);

    private Task DropAsync() => DropRequested(Row);
}
