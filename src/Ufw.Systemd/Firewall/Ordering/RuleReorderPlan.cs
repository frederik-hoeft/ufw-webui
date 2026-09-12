namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record RuleReorderPlan(IReadOnlySet<int> UntouchedOccurrences, IReadOnlyList<RuleReorderMove> Moves);
