namespace Ufw.Pipes.Client.Transport.Pipes.Unix;

internal sealed class UnixNamedPipeClientStreamFactory : INamedPipeClientStreamFactory
{
    public INamedPipeClientStreamDescriptor CreatePipeStreamDescriptor(string serverName, string pipeName) => 
        new UnixNamedPipeClientStreamDescriptor(serverName, pipeName);
}
