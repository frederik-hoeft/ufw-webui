namespace Ufw.Pipes.Client.Transport.Tcp;

internal sealed class TcpClientStreamFactory : ITcpClientStreamFactory
{
    public ITcpClientStreamDescriptor CreatePipeStreamDescriptor(string serverName, int port) => 
        new TcpClientStreamDescriptor(serverName, port);
}
