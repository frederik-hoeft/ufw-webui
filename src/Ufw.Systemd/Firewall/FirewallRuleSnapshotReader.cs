using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallRuleSnapshotReader(IUfwRunner ufwRunner, ILogger logger) : IFirewallRuleSnapshotReader
{
    private readonly ILogger<FirewallRuleSnapshotReader> _logger = logger.Scoped<FirewallRuleSnapshotReader>();

    public async Task<FirewallRuleSnapshotReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        UfwListCommand command = new();
        UfwProcessResult result;
        try
        {
            result = await ufwRunner.ExecuteAsync(command, cancellationToken);
        }
        catch (ChildProcessException exception)
        {
            _logger.LogError(exception, "Failed to start UFW while reading the current rule set.");
            return new FirewallRuleSnapshotReadResult(new InternalServerErrorResponse("Failed to start UFW while reading the current rule set."), null);
        }

        if (result.CancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException("The UFW subprocess was canceled after it started.", cancellationToken);
        }

        if (!result.Succeeded)
        {
            string diagnostics = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            _logger.LogError($"ufw status failed with exit code {result.ExitCode}: {diagnostics}");
            return new FirewallRuleSnapshotReadResult(new InternalServerErrorResponse("Failed to read the current UFW rule set."), null);
        }

        UfwStatusSnapshot? snapshot = await command.GetResultAsync(cancellationToken);
        if (snapshot is null)
        {
            _logger.LogError("UFW status returned successful process output that could not be parsed as a status response.");
            return new FirewallRuleSnapshotReadResult(new InternalServerErrorResponse("Failed to parse the current UFW rule set."), null);
        }

        return new FirewallRuleSnapshotReadResult(null, snapshot);
    }
}
