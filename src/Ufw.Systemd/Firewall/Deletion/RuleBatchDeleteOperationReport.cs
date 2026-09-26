namespace Ufw.Systemd.Firewall.Deletion;

internal sealed record RuleBatchDeleteOperationReport(int OccurrenceId, string? RuleId, RuleBatchDeleteOperationStatus Status, string? Diagnostic);
