namespace Ufw.Systemd.Network;

internal interface INetworkApplicationWorker
{
    Task ServeAsync(INetworkApplication manager, CancellationToken cancellationToken);
}
