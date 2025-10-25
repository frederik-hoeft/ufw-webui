using System.Net.Sockets;

namespace Ufw.Pipes.Client.Transport.Tcp;

public interface ITcpClientStreamDescriptor
{
    Task<NetworkStream> ConnectAsync(CancellationToken cancellationToken);

    Task<NetworkStream> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken);

    Task<NetworkStream> ConnectAsync(int timeout, CancellationToken cancellationToken);
}
