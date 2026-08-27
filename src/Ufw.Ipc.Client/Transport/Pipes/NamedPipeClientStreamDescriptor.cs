using System.IO.Pipes;
using System.Security.Principal;

namespace Ufw.Ipc.Client.Transport.Pipes;

internal sealed class NamedPipeClientStreamDescriptor(string serverName, string pipeName) : INamedPipeClientStreamDescriptor
{
    private NamedPipeClientStream CreateClientStream() => new
    (
        serverName,
        pipeName,
        direction: PipeDirection.InOut,
        options: PipeOptions.WriteThrough,
        impersonationLevel: TokenImpersonationLevel.Identification,
        inheritability: HandleInheritability.None
    );

    public async Task<NamedPipeClientStream> ConnectAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        NamedPipeClientStream clientStream = CreateClientStream();
        await clientStream.ConnectAsync(timeout, cancellationToken);
        return clientStream;
    }

    public Task<NamedPipeClientStream> ConnectAsync(CancellationToken cancellationToken) =>
        ConnectAsync(Timeout.InfiniteTimeSpan, cancellationToken);

    public Task<NamedPipeClientStream> ConnectAsync(int timeout, CancellationToken cancellationToken) =>
        ConnectAsync(TimeSpan.FromMilliseconds(timeout), cancellationToken);
}
