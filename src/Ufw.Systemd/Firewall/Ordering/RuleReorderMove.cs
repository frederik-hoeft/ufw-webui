namespace Ufw.Systemd.Firewall.Ordering;

/// <summary>
/// One logical remove/reinsert move. A null anchor places the occurrence at the end of the ordered rule set.
/// </summary>
internal sealed record RuleReorderMove(int OccurrenceId, int TargetIndex, int? BeforeOccurrenceId);
