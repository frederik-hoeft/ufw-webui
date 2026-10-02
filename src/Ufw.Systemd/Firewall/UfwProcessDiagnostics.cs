using Ufw.Systemd.Interop.IO;

namespace Ufw.Systemd.Firewall;

internal static class UfwProcessDiagnostics
{
    public static string Format(UfwProcessResult result, string operation)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        string details = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
        return result.CancellationRequested
            ? $"UFW process was canceled after it started while {operation}."
            : $"UFW process exited with code {result.ExitCode} while {operation}: {details}";
    }

    public static string? Combine(params string?[] diagnostics)
    {
        string[] nonEmpty = diagnostics.Where(static diagnostic => !string.IsNullOrWhiteSpace(diagnostic)).Select(static diagnostic => diagnostic!).ToArray();
        return nonEmpty.Length == 0 ? null : string.Join(' ', nonEmpty);
    }
}
