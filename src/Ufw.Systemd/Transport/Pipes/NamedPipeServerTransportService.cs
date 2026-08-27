using System.IO.Pipes;
using Ufw.Ipc.Shared.Transport;

namespace Ufw.Systemd.Transport.Pipes;

internal sealed class NamedPipeServerTransportService(INamedPipeServerStreamDescriptor serverPipeDescriptor) : ITransportLayerService
{
    public async Task<ITransportLayerConnection> ServeAsync(CancellationToken cancellationToken)
    {
        NamedPipeServerStream pipeStream = await serverPipeDescriptor.ServeAsync(cancellationToken);
        return new DefaultTransportConnection(pipeStream);
    }
}
