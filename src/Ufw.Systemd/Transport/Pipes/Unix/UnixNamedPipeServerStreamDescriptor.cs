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

    public async Task<NamedPipeServerStream> ServeAsync(CancellationToken cancellationToken)
    {
        string pipeName = configuration.Settings.Transport.Pipe?.PipeName ?? throw new InvalidOperationException("Pipe transport settings are not configured.");
        NamedPipeServerStream serverStream = new
        (
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.WriteThrough
        );

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(pipeName, SOCKET_MODE);
            }

            await serverStream.WaitForConnectionAsync(cancellationToken);
            return serverStream;
        }
        catch
        {
            await serverStream.DisposeAsync();
            throw;
        }
    }
}
