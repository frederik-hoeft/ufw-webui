namespace Ufw.Systemd.Firewall.Ordering;

internal interface IFirewallReorderExecutor
{
    Task<RuleReorderExecutionResult> ExecuteAsync(RuleReorderExecutionRequest request, FirewallSnapshot baseline, CancellationToken cancellationToken);
}
