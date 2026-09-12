namespace Ufw.Systemd.Firewall.Ordering;

internal interface IFirewallReorderRecoveryService
{
    Task RecoverAsync(CancellationToken cancellationToken);
}
