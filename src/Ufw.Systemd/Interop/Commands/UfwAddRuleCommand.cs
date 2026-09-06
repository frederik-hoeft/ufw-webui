using System.Collections.Immutable;
using Ufw.Firewall;
using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwAddRuleCommand(FirewallRuleSpecification specification, IUfwRuleCommandRenderer renderer) : IUfwCommand
{
    public ImmutableArray<string> BuildArguments() => ["--force", .. renderer.Render(specification).Arguments];

    public void SetOutput(string output)
    {
    }
}
