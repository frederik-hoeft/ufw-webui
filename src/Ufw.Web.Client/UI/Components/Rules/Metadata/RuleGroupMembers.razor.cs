using Microsoft.AspNetCore.Components;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class RuleGroupMembers
{
    [Parameter, EditorRequired]
    public RuleGroupManagementProjection Projection { get; set; } = null!;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<RuleGroup> EditRequested { get; set; }

    [Parameter]
    public EventCallback<RuleGroup> DeleteRequested { get; set; }
}
