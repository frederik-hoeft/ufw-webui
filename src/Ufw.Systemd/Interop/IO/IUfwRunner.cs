using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Interop.IO;

internal interface IUfwRunner
{
    Task<bool> RunAsync(IUfwCommand command, CancellationToken cancellationToken);
}
