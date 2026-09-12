using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Firewall.Insertion;

internal interface IFirewallOrderedInsertionService
{
    ValueTask<RuleInsertionExecutionResult> InsertAsync(InsertRulePayload payload, CancellationToken cancellationToken);
}
