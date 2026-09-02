using System.Collections.Immutable;
using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwDeleteRuleCommand(int displayNumber) : IUfwCommand
{
    public ImmutableArray<string> BuildArguments() => UfwRuleArgumentBuilder.BuildDeleteByNumber(displayNumber);

    public void SetOutput(string output)
    {
    }
}
