using Microsoft.AspNetCore.Components;
using Ufw.Web.Client.Features.Rules.Templates;

namespace Ufw.Web.Client.UI.Components.Rules.Templates;

public sealed partial class RuleTemplateActionsMenu
{
    [Parameter, EditorRequired]
    public RuleTemplate Template { get; set; } = null!;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<RuleTemplate> EditRequested { get; set; }

    [Parameter]
    public EventCallback<RuleTemplate> DeleteRequested { get; set; }
}
