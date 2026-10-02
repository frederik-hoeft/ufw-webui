using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Replacement;

/// <summary>
/// Executes a preflight-approved firewall rule replacement while reconciling every UFW mutation against authoritative state and restoring a safe state when possible.
/// </summary>
internal interface IFirewallRuleReplacementTransactionExecutor
{
    /// <summary>
    /// Executes the selected replacement strategy for <paramref name="preflight"/> against <paramref name="baseline"/> and returns the authoritative post-operation classification.
    /// </summary>
    Task<RuleReplacementExecutionResult> ExecuteAsync(RuleListResponse baseline, RuleReplacementPreflight preflight, CancellationToken cancellationToken);
}
