namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallReorderService(
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard,
    IFirewallReorderExecutor executor) : IFirewallReorderService
{
    public Task<RuleReorderExecutionResult> ReorderAsync(RuleReorderExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return executionGate.RunAsync(async ct =>
        {
            await mutationSafetyGuard.EnsureSafeAsync(ct);
            return await executor.ExecuteAsync(request, ct);
        }, cancellationToken);
    }
}
