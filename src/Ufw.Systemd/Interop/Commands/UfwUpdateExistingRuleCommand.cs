using System.Collections.Immutable;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwUpdateExistingRuleCommand(FirewallRuleSpecification specification, IUfwRuleCommandRenderer renderer) : IUfwCommand
{
    public ImmutableArray<string> BuildArguments()
    {
        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(specification);
        ImmutableArray<string> arguments = renderer.Render(normalized).Arguments;
        return normalized.Comment is null ? [.. arguments, "comment", string.Empty] : arguments;
    }

    public void SetOutput(string output)
    {
    }
}
