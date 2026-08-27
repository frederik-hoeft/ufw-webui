using System.Net.Sockets;

namespace Ufw.Systemd.Transport.Tcp;

internal interface ITcpServerStreamDescriptor
{
    Task<NetworkStream> ServeAsync(CancellationToken cancellationToken);
}
