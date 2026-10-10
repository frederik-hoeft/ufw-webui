using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.UI.Components.Rules;

/// <summary>
/// Owns the expansion state of one rendered rule occurrence. Inventory and ordering authority remain with the workspace.
/// </summary>
internal sealed class RuleRowInteractionState
{
    public bool MetadataExpanded { get; private set; }

    public static bool DetailsAvailable(RuleRowProjection row) =>
        !string.IsNullOrWhiteSpace(row.Rule.RuleId) || !string.IsNullOrWhiteSpace(row.CanonicalCommand);

    public void ToggleMetadata(RuleRowProjection row)
    {
        if (DetailsAvailable(row))
        {
            MetadataExpanded = !MetadataExpanded;
        }
    }

    public void HandleKeyDown(RuleRowProjection row, string? key)
    {
        if (key is "Enter" or " ")
        {
            ToggleMetadata(row);
        }
    }

    public static bool CanDrag(RuleRowProjection row, bool orderingDisabled) => !orderingDisabled && row.CanOrder;

    public static string DragHandleClass(RuleRowProjection row, bool orderingDisabled) => CanDrag(row, orderingDisabled)
        ? "rule-drag-handle"
        : "rule-drag-handle rule-drag-handle-disabled";
}
