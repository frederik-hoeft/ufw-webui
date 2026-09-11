using Microsoft.AspNetCore.Components;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Client.Components.Rules;

public sealed partial class UfwRuleCommandPreview
{
    [Parameter, EditorRequired]
    public FirewallRuleSpecification Rule { get; set; } = null!;

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public RenderFragment? Details { get; set; }

    private string EffectiveLabel => string.IsNullOrWhiteSpace(Label) ? RulesText["UfwRule"] : Label;

    private bool TryGetCommandText(out string? commandText)
    {
        if (!UfwRuleCommandRenderer.TryRender(Rule, out UfwRenderedRule? renderedRule))
        {
            commandText = null;
            return false;
        }

        commandText = renderedRule.DisplayText;
        return true;
    }
}
