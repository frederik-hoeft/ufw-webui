using System.Collections.Immutable;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwVerboseStatusCommand : IUfwCommand
{
    private static readonly ImmutableArray<string> s_arguments = ["status", "verbose"];

    public ImmutableArray<string> BuildArguments() => s_arguments;
}
