using System.IO.Pipes;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Shared.Transport;

namespace Ufw.Ipc.Client.Transport.Pipes;

internal sealed class NamedPipeClientTransportService(UfwClientOptions options, INamedPipeClientStreamFactory pipeStreamFactory) : ITransportLayerService
{
    private readonly INamedPipeClientStreamDescriptor _pipeDescriptor = pipeStreamFactory.CreatePipeStreamDescriptor(options.ServerName, options.PipeName);

    public async Task<ITransportLayerConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        NamedPipeClientStream pipeStream = await _pipeDescriptor.ConnectAsync(cancellationToken);
        return new DefaultTransportConnection(pipeStream);
    }
}
