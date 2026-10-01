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

    private string ActionClass(FirewallAction action)
    {
        string semanticClass = action switch
        {
            FirewallAction.Allow => "rule-action rule-action-allow",
            FirewallAction.Deny => "rule-action rule-action-deny",
            FirewallAction.Reject => "rule-action rule-action-reject",
            FirewallAction.Limit => "rule-action rule-action-limit",
            _ => "rule-action",
        };

        return EmphasizeAction ? semanticClass : $"{semanticClass} rule-action-normal";
    }
}
