namespace Ufw.Ipc.Client.Transport.Pipes;

public interface INamedPipeClientStreamFactory
{
    INamedPipeClientStreamDescriptor CreatePipeStreamDescriptor(string serverName, string pipeName);
}
