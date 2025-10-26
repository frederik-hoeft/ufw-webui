using System.IO.Pipes;
using Ufw.Pipes.Client.Configuration;
using Ufw.Pipes.Shared;
using Ufw.Pipes.Shared.Transport;

namespace Ufw.Pipes.Client.Transport.Pipes;

internal sealed class NamedPipeClientTransportService(UfwClientOptions options, INamedPipeClientStreamFactory pipeStreamFactory) : ITransportLayerService
{
    private readonly INamedPipeClientStreamDescriptor _pipeDescriptor = pipeStreamFactory.CreatePipeStreamDescriptor(options.ServerName, options.PipeName);

    public async Task<ITransportLayerConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        NamedPipeClientStream pipeStream = await _pipeDescriptor.ConnectAsync(cancellationToken);
        return new DefaultTransportConnection(pipeStream);
    }
}
