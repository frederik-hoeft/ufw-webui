namespace Ufw.Systemd.Firewall;

internal interface IFirewallMutationSafetyGuard
{
    Task EnsureSafeAsync(CancellationToken cancellationToken);
}
