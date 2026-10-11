using Microsoft.AspNetCore.Components;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules;

public sealed partial class RuleDesktopContent
{
    [Parameter, EditorRequired]
    public FirewallRuleSpecification Rule { get; set; } = null!;

    [Parameter]
    public IReadOnlyList<RuleTag> Tags { get; set; } = [];

    [Parameter]
    public bool EmphasizeAction { get; set; } = true;

}
