using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.UI.Components.Rules;

public sealed record RuleInsertionActionRequest(ListedFirewallRule Rule, RuleInsertionPlacement Placement);
