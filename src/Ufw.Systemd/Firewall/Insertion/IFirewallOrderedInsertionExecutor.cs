using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Firewall.Insertion;

internal interface IFirewallOrderedInsertionExecutor
{
    Task<RuleInsertionExecutionResult> ExecuteAsync(InsertRulePayload payload, FirewallSnapshot baseline, CancellationToken cancellationToken);
}
