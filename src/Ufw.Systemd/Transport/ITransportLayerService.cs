using Ufw.Shared.Ipc.Transport;

namespace Ufw.Systemd.Transport;

internal interface ITransportLayerService
{
    Task<ITransportLayerConnection> ServeAsync(CancellationToken cancellationToken);
}
