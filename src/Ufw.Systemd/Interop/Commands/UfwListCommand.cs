using System.Collections.Immutable;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwListCommand : IUfwCommand
{
    private static readonly ImmutableArray<string> s_arguments = ["status", "numbered"];

    public ImmutableArray<string> BuildArguments() => s_arguments;

    public void SetOutput(string output) => throw new NotImplementedException();
}
