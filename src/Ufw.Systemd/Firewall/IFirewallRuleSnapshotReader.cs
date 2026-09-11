namespace Ufw.Systemd.Firewall;

internal interface IFirewallRuleSnapshotReader
{
    Task<FirewallRuleSnapshotReadResult> ReadAsync(CancellationToken cancellationToken);
}
