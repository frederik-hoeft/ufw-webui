using Ufw.Pipes.Client.Transport.Pipes;

namespace Ufw.Pipes.Client.Transport.Tcp;

public interface ITcpClientStreamFactory
{
    ITcpClientStreamDescriptor CreatePipeStreamDescriptor(string serverName, int port);
}
