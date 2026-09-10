using System.IO.Pipes;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Transport.Pipes.Unix;

internal sealed class UnixNamedPipeServerStreamDescriptor(IConfiguration configuration) : INamedPipeServerStreamDescriptor
{
    private const UnixFileMode SOCKET_MODE =
        UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.GroupRead
        | UnixFileMode.GroupWrite;

    private NamedPipeServerStream CreateServerStream()
    {
        string pipeName = configuration.Settings.Pipe.PipeName;
        NamedPipeServerStream stream = new
        (
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.WriteThrough
        );

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(pipeName, SOCKET_MODE);
        }

        return stream;
    }

    public async Task<NamedPipeServerStream> ServeAsync(CancellationToken cancellationToken)
    {
        NamedPipeServerStream serverStream = CreateServerStream();
        await serverStream.WaitForConnectionAsync(cancellationToken);
        return serverStream;
    }
}
