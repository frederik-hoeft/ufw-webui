using System.Collections.Immutable;

namespace Ufw.Systemd.Interop.IO;

internal sealed record ChildProcessRequest(
    string Command,
    ImmutableArray<string> Arguments,
    ImmutableDictionary<string, string> Environment);
