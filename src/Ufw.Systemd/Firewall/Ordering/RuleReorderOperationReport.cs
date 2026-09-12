namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record RuleReorderOperationReport(
    RuleReorderMove Move,
    RuleReorderOperationStatus Status,
    string? Diagnostic);
