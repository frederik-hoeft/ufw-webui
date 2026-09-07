using Ufw.Shared.Firewall;
namespace Ufw.Client.Components.Rules;

internal static class FirewallRulePresentation
{
    public static string ActionClass(FirewallAction action) => action switch
    {
        FirewallAction.Allow => "rule-action rule-action-allow",
        FirewallAction.Deny => "rule-action rule-action-deny",
        FirewallAction.Reject => "rule-action rule-action-reject",
        FirewallAction.Limit => "rule-action rule-action-limit",
        _ => "rule-action",
    };
}
