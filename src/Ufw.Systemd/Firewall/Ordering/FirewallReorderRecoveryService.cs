namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallReorderRecoveryService(
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard) : IFirewallReorderRecoveryService
{
    public Task RecoverAsync(CancellationToken cancellationToken) =>
        executionGate.RunAsync(RecoverUnderGateAsync, cancellationToken);

    private async Task<bool> RecoverUnderGateAsync(CancellationToken cancellationToken)
    {
        await mutationSafetyGuard.EnsureSafeAsync(cancellationToken);
        return true;
    }
}
