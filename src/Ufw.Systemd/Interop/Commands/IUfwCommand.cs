using System.Collections.Immutable;

namespace Ufw.Systemd.Interop.Commands;

internal interface IUfwCommand
{
    ImmutableArray<string> BuildArguments();

    void SetOutput(string output);
}
