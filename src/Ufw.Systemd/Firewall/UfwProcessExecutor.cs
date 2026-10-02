using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall;

internal sealed class UfwProcessExecutor(IUfwRunner ufwRunner, ILogger logger) : IUfwProcessExecutor
{
    private readonly ILogger<UfwProcessExecutor> _logger = logger.Scoped<UfwProcessExecutor>();

    public async Task<UfwProcessExecutionResult> ExecuteAsync(IUfwCommand command, string operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        try
        {
            UfwProcessResult result = await ufwRunner.ExecuteAsync(command, cancellationToken);
            return new UfwProcessExecutionResult(
                result.Succeeded,
                result.CancellationRequested,
                result.Succeeded ? null : UfwProcessDiagnostics.Format(result, operation),
                RunnerFailed: false);
        }
        catch (ChildProcessException exception)
        {
            _logger.LogError(exception, $"UFW execution failed while {operation}. Authoritative state reconciliation determines the operation outcome.");
            return new UfwProcessExecutionResult(Succeeded: false, CancellationRequested: false, exception.Message, RunnerFailed: true);
        }
    }
}
