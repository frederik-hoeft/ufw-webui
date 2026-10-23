namespace Ufw.Systemd.Firewall.SystemState;

internal interface IFilterSystemStateReader
{
    ValueTask<FilterSystemStateReadResult> ReadAsync(CancellationToken cancellationToken);
}
