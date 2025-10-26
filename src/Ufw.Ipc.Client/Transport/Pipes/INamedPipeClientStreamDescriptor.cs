using System.IO.Pipes;

namespace Ufw.Ipc.Client.Transport.Pipes;

public interface INamedPipeClientStreamDescriptor
{
    Task<NamedPipeClientStream> ConnectAsync(CancellationToken cancellationToken);

    Task<NamedPipeClientStream> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

    Task<NamedPipeClientStream> ConnectAsync(int timeout, CancellationToken cancellationToken);
}
