using System.IO.Pipes;

namespace Ufw.Systemd.Transport.Pipes;

internal interface INamedPipeServerStreamDescriptor
{
    Task<NamedPipeServerStream> ServeAsync(CancellationToken cancellationToken);
}
