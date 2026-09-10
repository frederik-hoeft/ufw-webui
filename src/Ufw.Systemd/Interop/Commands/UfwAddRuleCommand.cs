using System.Collections.Immutable;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwAddRuleCommand(FirewallRuleSpecification specification, IUfwRuleCommandRenderer renderer) : IUfwCommand
{
    public ImmutableArray<string> BuildArguments() => renderer.Render(specification).Arguments;

    public void SetOutput(string output)
    {
    }
}
