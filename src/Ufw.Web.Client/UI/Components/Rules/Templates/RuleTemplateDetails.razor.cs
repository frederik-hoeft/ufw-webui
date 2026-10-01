using Microsoft.AspNetCore.Components;
using Ufw.Web.Client.Features.Rules.Templates;

namespace Ufw.Web.Client.UI.Components.Rules.Templates;

public sealed partial class RuleTemplateDetails
{
    [Parameter, EditorRequired]
    public RuleTemplate Template { get; set; } = null!;

    [Parameter, EditorRequired]
    public string CanonicalCommand { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? Actions { get; set; }
}
