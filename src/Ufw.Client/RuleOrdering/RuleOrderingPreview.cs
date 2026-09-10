using Ufw.Client.Api;
using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleOrdering;

public sealed class RuleOrderingPreview
{
    public RuleOrderingPreview(
        IReadOnlyList<ListedFirewallRule> rules,
        IReadOnlyDictionary<string, int> originalPositions,
        IReadOnlySet<string> directlyMovedRuleIds,
        IReadOnlyList<RuleMoveRequest> moves)
    {
        Rules = rules;
        OriginalPositions = originalPositions;
        DirectlyMovedRuleIds = directlyMovedRuleIds;
        Moves = moves;
    }

    public IReadOnlyList<ListedFirewallRule> Rules { get; }

    public IReadOnlyDictionary<string, int> OriginalPositions { get; }

    public IReadOnlySet<string> DirectlyMovedRuleIds { get; }

    public IReadOnlyList<RuleMoveRequest> Moves { get; }

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
