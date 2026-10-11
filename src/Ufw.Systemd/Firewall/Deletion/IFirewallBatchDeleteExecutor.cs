using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Firewall.Deletion;

internal interface IFirewallBatchDeleteExecutor
{
    Task<RuleBatchDeleteExecutionResult> ExecuteAsync(BatchDeleteRulesPayload payload, FirewallSnapshot baseline, CancellationToken cancellationToken);
}
