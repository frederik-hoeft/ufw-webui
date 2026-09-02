using System.Collections.Immutable;

namespace Ufw.Systemd.Interop.IO;

internal sealed record UfwProcessResult(int ExitCode, string Output, ImmutableArray<string> Arguments)
{
    public bool Succeeded => ExitCode == 0;
}
