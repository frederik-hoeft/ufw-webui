using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Systemd.Firewall.Ordering;

/// <summary>
/// Estimates the relative cost of reinserting a rendered rule. Costs must be non-negative and only break ties between minimum-move reorder plans.
/// </summary>
internal interface IRuleReinsertionCostProvider
{
    int GetReinsertionCost(UfwRenderedRule renderedRule);
}
