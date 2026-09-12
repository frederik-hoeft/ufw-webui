namespace Ufw.Systemd.Firewall.Ordering;

internal interface IRuleReorderPlanner
{
    RuleReorderPlan Plan(
        IReadOnlyList<int> currentOrder,
        IReadOnlyList<int> desiredOrder,
        IReadOnlySet<int> immutableOccurrences,
        IReadOnlyDictionary<int, int>? keepPriorities = null);
}
