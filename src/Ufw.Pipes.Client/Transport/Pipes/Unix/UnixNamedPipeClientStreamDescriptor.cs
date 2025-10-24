using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Ufw.Pipes.Shared;

namespace Ufw.Pipes.Client.Transport.Pipes.Unix;

internal sealed class UnixNamedPipeClientStreamDescriptor(string serverName, string pipeName) : INamedPipeClientStreamDescriptor
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
        await clientStream.ConnectAsync(timeout, cancellationToken).NoCapture();
        return clientStream;
    }
    
    public Task<NamedPipeClientStream> ConnectAsync(CancellationToken cancellationToken) => 
        ConnectAsync(Timeout.InfiniteTimeSpan, cancellationToken);

    public Task<NamedPipeClientStream> ConnectAsync(int timeout, CancellationToken cancellationToken) => 
        ConnectAsync(TimeSpan.FromMilliseconds(timeout), cancellationToken);
}
