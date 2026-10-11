using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.UI.Components.Rules;

/// <summary>Maps firewall action semantics to the shared desktop/mobile presentation classes.</summary>
internal static class RuleActionAppearance
{
    public static string CssClass(FirewallAction action, bool emphasize = true)
    {
        string semanticClass = action switch
        {
            FirewallAction.Allow => "rule-action rule-action-allow",
            FirewallAction.Deny => "rule-action rule-action-deny",
            FirewallAction.Reject => "rule-action rule-action-reject",
            FirewallAction.Limit => "rule-action rule-action-limit",
            _ => "rule-action",
        };

        return emphasize ? semanticClass : $"{semanticClass} rule-action-normal";
    }
}
