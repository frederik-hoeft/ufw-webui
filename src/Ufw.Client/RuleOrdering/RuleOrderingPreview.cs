using Ufw.Client.Api;
using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

public sealed class RuleOrderingPreview
(
    IReadOnlyList<ListedFirewallRule> rules,
    IReadOnlyDictionary<string, int> originalPositions,
    IReadOnlySet<string> directlyMovedRuleIds,
    IReadOnlyList<RuleMoveRequest> moves
)
{
    public IReadOnlyList<ListedFirewallRule> Rules { get; } = rules;

    public IReadOnlyDictionary<string, int> OriginalPositions { get; } = originalPositions;

    public IReadOnlySet<string> DirectlyMovedRuleIds { get; } = directlyMovedRuleIds;

    public IReadOnlyList<RuleMoveRequest> Moves { get; } = moves;

    public int? GetOriginalPosition(ListedFirewallRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return !string.IsNullOrWhiteSpace(rule.RuleId)
            && OriginalPositions.TryGetValue(rule.RuleId, out int position)
                ? position
                : null;
    }

    public bool WasDirectlyMoved(ListedFirewallRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return !string.IsNullOrWhiteSpace(rule.RuleId) && DirectlyMovedRuleIds.Contains(rule.RuleId);
    }
}
