namespace Ufw.Systemd.Firewall.Ordering;

internal enum RuleReorderExecutionOutcome
{
    Completed,
    StaleBaseline,
    PreconditionFailed,
    PartiallyCompleted,
    RecoveryFailed,
    StateUncertain,
}
