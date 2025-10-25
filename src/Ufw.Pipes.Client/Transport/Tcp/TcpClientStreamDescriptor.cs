using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Ufw.Pipes.Client.Transport.Tcp;

internal sealed class TcpClientStreamDescriptor(string serverName, int port) : ITcpClientStreamDescriptor
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
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
