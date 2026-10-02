namespace Ufw.Systemd.Firewall;

internal sealed record UfwProcessExecutionResult(bool Succeeded, bool CancellationRequested, string? Diagnostic, bool RunnerFailed);
