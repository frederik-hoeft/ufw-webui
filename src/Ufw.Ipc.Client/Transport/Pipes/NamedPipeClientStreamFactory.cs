namespace Ufw.Ipc.Client.Transport.Pipes;

internal sealed class NamedPipeClientStreamFactory : INamedPipeClientStreamFactory
{
    public INamedPipeClientStreamDescriptor CreatePipeStreamDescriptor(string serverName, string pipeName) =>
        new NamedPipeClientStreamDescriptor(serverName, pipeName);
}
