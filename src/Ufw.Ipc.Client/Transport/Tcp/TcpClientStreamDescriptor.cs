using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Ufw.Ipc.Client.Transport.Tcp;

internal sealed class TcpClientStreamDescriptor(string serverName, int port) : ITcpClientStreamDescriptor
{
    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    public async Task<NetworkStream> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(serverName, port, cancellationToken);
        NetworkStream networkStream = tcpClient.GetStream();
        return networkStream;
    }
    
    public Task<NetworkStream> ConnectAsync(CancellationToken cancellationToken) => 
        ConnectAsync(Timeout.InfiniteTimeSpan, cancellationToken);

    public Task<NetworkStream> ConnectAsync(int timeout, CancellationToken cancellationToken) => 
        ConnectAsync(TimeSpan.FromMilliseconds(timeout), cancellationToken);
}
