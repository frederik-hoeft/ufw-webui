using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Systemd.Firewall.Ordering;

/// <summary>
/// Estimates rule reinsertion cost from the number of rendered UFW arguments.
/// </summary>
internal sealed class UfwArgumentCountReinsertionCostProvider : IRuleReinsertionCostProvider
{
    public int GetReinsertionCost(UfwRenderedRule renderedRule)
    {
        ArgumentNullException.ThrowIfNull(renderedRule);
        return renderedRule.Arguments.Length;
    }
}
