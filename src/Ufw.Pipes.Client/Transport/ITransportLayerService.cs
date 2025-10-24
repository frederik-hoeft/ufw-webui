using Ufw.Pipes.Shared.Transport;

namespace Ufw.Pipes.Client.Transport;

public interface ITransportLayerService
{
    Task<ITransportLayerConnection> ConnectAsync(CancellationToken cancellationToken);
}
