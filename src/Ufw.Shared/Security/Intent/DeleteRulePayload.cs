using Ufw.Shared.Firewall;

namespace Ufw.Shared.Security.Intent;

public sealed class DeleteRulePayload
{
    public required string RuleId { get; set; }

    public required FirewallRuleSpecification Rule { get; set; }
}
