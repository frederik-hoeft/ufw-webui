namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallReorderRecoveryService(IUfwExecutionGate executionGate, IFirewallMutationSafetyGuard mutationSafetyGuard) : IFirewallReorderRecoveryService
{
    public Task RecoverAsync(CancellationToken cancellationToken) =>
        executionGate.RunAsync(mutationSafetyGuard.EnsureSafeAsync, cancellationToken);
}
