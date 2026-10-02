using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Firewall.Replacement;

/// <summary>
/// Coordinates authoritative baseline validation, replacement preflight, and execution for one signed firewall rule replacement request.
/// </summary>
internal interface IFirewallRuleReplacementExecutor
{
    /// <summary>
    /// Executes <paramref name="payload"/> against the current authoritative firewall state and returns the reconciled replacement outcome.
    /// </summary>
    Task<RuleReplacementExecutionResult> ExecuteAsync(ReplaceRulePayload payload, CancellationToken cancellationToken);
}
