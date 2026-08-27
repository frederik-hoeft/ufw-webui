using System.IO.Pipes;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Transport.Pipes.Unix;

internal sealed class UnixNamedPipeServerStreamDescriptor(IConfiguration configuration) : INamedPipeServerStreamDescriptor
{
    private NamedPipeServerStream CreateServerStream() => new
    (
        configuration.Settings.Pipe.PipeName,
        PipeDirection.InOut,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.WriteThrough
    );

    public async Task<NamedPipeServerStream> ServeAsync(CancellationToken cancellationToken)
    {
        NamedPipeServerStream serverStream = CreateServerStream();
        await serverStream.WaitForConnectionAsync(cancellationToken);
        return serverStream;
    }
}
