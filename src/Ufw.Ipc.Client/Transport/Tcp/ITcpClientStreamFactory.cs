namespace Ufw.Ipc.Client.Transport.Tcp;

public interface ITcpClientStreamFactory
{
    ITcpClientStreamDescriptor CreatePipeStreamDescriptor(string serverName, int port);
}
