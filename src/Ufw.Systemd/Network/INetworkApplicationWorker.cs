namespace Ufw.Systemd.Network;

internal interface INetworkApplicationWorker
{
    Task ServeAsync(CancellationToken cancellationToken);
}
