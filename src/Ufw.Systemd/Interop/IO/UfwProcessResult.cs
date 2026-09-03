using System.Collections.Immutable;

namespace Ufw.Systemd.Interop.IO;

internal sealed record UfwProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    ImmutableArray<string> Arguments,
    bool CancellationRequested)
{
    public bool Succeeded => ExitCode == 0;
}
