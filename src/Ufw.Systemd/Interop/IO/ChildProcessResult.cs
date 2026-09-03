namespace Ufw.Systemd.Interop.IO;

internal sealed record ChildProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool CancellationRequested);
