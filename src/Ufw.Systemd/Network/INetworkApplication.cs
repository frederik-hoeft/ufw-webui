namespace Ufw.Systemd.Network;

internal interface INetworkApplication
{
    Task RunAsync(CancellationToken cancellationToken);
}
