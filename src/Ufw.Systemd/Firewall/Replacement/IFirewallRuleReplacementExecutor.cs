using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Firewall.Replacement;

internal interface IFirewallRuleReplacementExecutor
{
    Task<RuleReplacementExecutionResult> ExecuteAsync(ReplaceRulePayload payload, CancellationToken cancellationToken);
}
