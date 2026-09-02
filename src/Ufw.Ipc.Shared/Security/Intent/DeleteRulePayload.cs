using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Ipc.Shared.Security.Intent;

public sealed class DeleteRulePayload
{
    public required string RuleId { get; set; }

    public required FirewallRuleSpecification Rule { get; set; }
}
