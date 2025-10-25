using System.Net.Sockets;
using Ufw.Pipes.Shared.Transport;

namespace Ufw.Systemd.Transport.Tcp;

internal sealed class TcpServerTransportService(ITcpServerStreamDescriptor serverStreamDescriptor) : ITransportLayerService
{
    public async Task<ITransportLayerConnection> ServeAsync(CancellationToken cancellationToken)
    {
        NetworkStream networkStream =  await serverStreamDescriptor.ServeAsync(cancellationToken);
        return new DefaultTransportConnection(networkStream);
    }
}