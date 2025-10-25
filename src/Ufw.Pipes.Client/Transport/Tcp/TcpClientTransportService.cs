using System.Net.Sockets;
using Ufw.Pipes.Client.Configuration;
using Ufw.Pipes.Shared;
using Ufw.Pipes.Shared.Transport;

namespace Ufw.Pipes.Client.Transport.Tcp;

internal sealed class TcpClientTransportService(UfwClientOptions options, ITcpClientStreamFactory pipeStreamFactory) : ITransportLayerService
{
    private readonly ITcpClientStreamDescriptor _pipeDescriptor = pipeStreamFactory.CreatePipeStreamDescriptor("localhost", 1234);

    public async Task<ITransportLayerConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        NetworkStream networkStream = await _pipeDescriptor.ConnectAsync(cancellationToken).NoCapture();
        return new DefaultTransportConnection(networkStream);
    }
}
