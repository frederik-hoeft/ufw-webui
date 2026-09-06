using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;

namespace Ufw.Shared.Firewall.Rendering;

/// <summary>
/// Renders a validated firewall rule into the canonical UFW rule syntax shared by subprocess dispatch and user-facing confirmation UI.
/// </summary>
public interface IUfwRuleCommandRenderer
{
    UfwRenderedRule Render(FirewallRuleSpecification specification);

    bool TryRender(FirewallRuleSpecification specification, [NotNullWhen(true)] out UfwRenderedRule? renderedRule);
}
