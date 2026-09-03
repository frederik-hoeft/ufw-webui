using System.Collections.Immutable;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwAddRuleCommand(FirewallRuleSpecification specification) : IUfwCommand
{
    public ImmutableArray<string> BuildArguments() => UfwRuleArgumentBuilder.BuildAdd(specification);

    public void SetOutput(string output)
    {
    }
}
