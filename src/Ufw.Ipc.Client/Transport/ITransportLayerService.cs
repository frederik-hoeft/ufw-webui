using Ufw.Shared.Ipc.Transport;

namespace Ufw.Ipc.Client.Transport;

public interface ITransportLayerService
{
    Task<ITransportLayerConnection> ConnectAsync(CancellationToken cancellationToken);
}
