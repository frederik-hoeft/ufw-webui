using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall.Ordering;

internal interface IRuleReinsertabilityClassifier
{
    RuleReinsertability Classify(ListedFirewallRule rule);
}
