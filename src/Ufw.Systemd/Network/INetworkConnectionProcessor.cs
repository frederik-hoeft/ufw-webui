using Ufw.Shared.Ipc.Transport;

namespace Ufw.Systemd.Network;

internal interface INetworkConnectionProcessor
{
    Task ProcessAsync(ITransportLayerConnection connection, Guid workerId, CancellationToken cancellationToken);
}
