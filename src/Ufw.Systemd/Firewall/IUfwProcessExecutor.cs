using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Firewall;

internal interface IUfwProcessExecutor
{
    Task<UfwProcessExecutionResult> ExecuteAsync(IUfwCommand command, string operation, CancellationToken cancellationToken);
}
