using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Ipc.Shared.Security.Intent;

public sealed class AddRulePayload
{
    public required FirewallRuleSpecification Rule { get; set; }
}
