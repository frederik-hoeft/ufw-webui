using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record RuleReorderPreflight(
    IReadOnlyList<int> DesiredOrder,
    RuleReorderPlan Plan,
    IReadOnlyDictionary<int, FirewallRuleSpecification> MoveSpecifications,
    IReadOnlySet<int> ImmutableOccurrences,
    IReadOnlyDictionary<int, int> ReinsertionCosts);
