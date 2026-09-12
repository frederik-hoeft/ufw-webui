using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

public sealed record RuleInsertionActionRequest(
    ListedFirewallRule Rule,
    RuleInsertionPlacement Placement);
