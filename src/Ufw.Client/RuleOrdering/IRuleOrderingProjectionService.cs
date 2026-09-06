using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

public interface IRuleOrderingProjectionService
{
    IReadOnlyList<ListedFirewallRule> Move(
        IReadOnlyList<ListedFirewallRule> rules,
        string ruleId,
        int targetPosition);
}
