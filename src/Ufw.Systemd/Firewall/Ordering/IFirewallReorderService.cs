namespace Ufw.Systemd.Firewall.Ordering;

internal interface IFirewallReorderService
{
    Task<RuleReorderExecutionResult> ReorderAsync(RuleReorderExecutionRequest request, CancellationToken cancellationToken);
}
