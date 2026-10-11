using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.Configuration;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallRuleSnapshotReader(IUfwRunner ufwRunner, IUfwDefaultsReader defaultsReader, ILogger logger) : IFirewallRuleSnapshotReader
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
            return Error("Failed to start UFW while reading the current rule set.");
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
            return Error("Failed to read the current UFW rule set.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        UfwStatusSnapshot? snapshot = UfwStatusParser.Parse(result.StandardOutput);
        if (snapshot is null)
        {
            _logger.LogError("UFW status returned successful process output that could not be parsed as a status response.");
            return Error("Failed to parse the current UFW rule set.");
        }

        // The verbose view reports effective defaults. UFW does not support combining it with numbered output,
        // so verify that both invocations observed the same ordered rule list before accepting either view.
        UfwProcessResult verboseResult;
        try
        {
            verboseResult = await ufwRunner.ExecuteAsync(new UfwVerboseStatusCommand(), cancellationToken);
        }
        catch (ChildProcessException exception)
        {
            _logger.LogError(exception, "Failed to start UFW verbose status query.");
            return Error("Failed to read effective UFW policies.");
        }
        if (verboseResult.CancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException("The UFW verbose status query was canceled after starting.", cancellationToken);
        }
        if (!verboseResult.Succeeded
            || !UfwVerboseStatusParser.TryParse(verboseResult.StandardOutput, snapshot,
                out (FirewallDefaultPolicy Incoming, FirewallDefaultPolicy Outgoing, FirewallDefaultPolicy Routed)? policies))
        {
            _logger.LogError("The numbered and verbose UFW status views could not be reconciled.");
            return Error("Failed to read a consistent authoritative UFW status.");
        }

        // Verbose status contains no IPv6 management-capability indicator, and inactive status omits defaults.
        // Read those unavailable settings from UFW's configured defaults; never substitute configured policies
        // for effective policies when the firewall is active.
        FirewallConfigurationSnapshot? configured = await defaultsReader.ReadAsync(cancellationToken);
        if (configured is null)
        {
            return Error("Failed to read the UFW IPv6 capability and inactive defaults.");
        }
        FirewallConfigurationSnapshot configuration = policies is { } effective
            ? new(configured.IPv6Enabled, effective.Incoming, effective.Outgoing, effective.Routed)
            : configured;

        return new FirewallRuleSnapshotReadResult.Success(FirewallRuleSet.FromStatus(snapshot, configuration));
    }

    private static FirewallRuleSnapshotReadResult Error(string message) =>
        new FirewallRuleSnapshotReadResult.Failure(message);
}
