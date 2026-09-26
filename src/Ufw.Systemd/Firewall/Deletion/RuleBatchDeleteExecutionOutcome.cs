namespace Ufw.Systemd.Firewall.Deletion;

internal enum RuleBatchDeleteExecutionOutcome
{
    Completed,
    StaleBaseline,
    PreconditionFailed,
    PartiallyCompleted,
    StateUncertain,
}
