using System.Collections.Immutable;

namespace Ufw.Systemd.Interop.IO;

internal interface IChildProcessRunner
{
    Task<int> RunAsync(string command, ImmutableArray<string> arguments, Out<string> output, CancellationToken cancellationToken);
}