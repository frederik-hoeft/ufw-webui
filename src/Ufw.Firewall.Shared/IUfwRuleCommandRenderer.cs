using System.Diagnostics.CodeAnalysis;
using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Firewall;

/// <summary>
/// Renders a validated firewall rule into the canonical UFW rule syntax shared by subprocess dispatch and user-facing confirmation UI.
/// </summary>
public interface IUfwRuleCommandRenderer
{
    UfwRenderedRule Render(FirewallRuleSpecification specification);

    bool TryRender(FirewallRuleSpecification specification, [NotNullWhen(true)] out UfwRenderedRule? renderedRule);
}
