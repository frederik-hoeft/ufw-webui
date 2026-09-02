using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Interop.IO;

internal interface IUfwRunner
{
    Task<UfwProcessResult> ExecuteAsync(IUfwCommand command, CancellationToken cancellationToken);
}
