using Microsoft.AspNetCore.Components;

namespace Ufw.Web.Client.UI.Components.Rules;

public sealed partial class RuleDesktopTableHeader
{
    [Parameter]
    public bool ShowOrderingColumns { get; set; } = true;

    [Parameter, EditorRequired]
    public string ActionsLabel { get; set; } = string.Empty;
}
