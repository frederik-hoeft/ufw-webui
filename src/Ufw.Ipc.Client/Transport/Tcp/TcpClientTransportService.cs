using System.Net.Sockets;
using Ufw.Ipc.Shared.Transport;

namespace Ufw.Ipc.Client.Transport.Tcp;

internal sealed class TcpClientTransportService(ITcpClientStreamFactory pipeStreamFactory) : ITransportLayerService
{
    private readonly ITcpClientStreamDescriptor _pipeDescriptor = pipeStreamFactory.CreatePipeStreamDescriptor("localhost", 1234);

    public async Task<ITransportLayerConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        NetworkStream networkStream = await _pipeDescriptor.ConnectAsync(cancellationToken);
        return new DefaultTransportConnection(networkStream);
    }
}
