using Ufw.Shared.Firewall;

namespace Ufw.Shared.Security.Intent;

public sealed class AddRulePayload
{
    public required FirewallRuleSpecification Rule { get; set; }
}
