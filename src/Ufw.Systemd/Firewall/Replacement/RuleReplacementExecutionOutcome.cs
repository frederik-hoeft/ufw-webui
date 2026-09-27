namespace Ufw.Systemd.Firewall.Replacement;

internal enum RuleReplacementExecutionOutcome
{
    Completed,
    StaleBaseline,
    PreconditionFailed,
    PartiallyCompleted,
    StateUncertain,
}
