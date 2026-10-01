using System.Net.Sockets;
using Ufw.Shared.Ipc.Transport;

namespace Ufw.Systemd.Transport.Tcp;

internal sealed class TcpServerTransportService(ITcpServerStreamDescriptor serverStreamDescriptor) : ITcpTransportLayerService
{
    public async Task<ITransportLayerConnection> ServeAsync(CancellationToken cancellationToken)
    {
        NetworkStream networkStream = await serverStreamDescriptor.ServeAsync(cancellationToken);
        return new DefaultTransportConnection(networkStream);
    }
}
