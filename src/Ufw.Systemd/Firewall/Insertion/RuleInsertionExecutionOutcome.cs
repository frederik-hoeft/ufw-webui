namespace Ufw.Systemd.Firewall.Insertion;

internal enum RuleInsertionExecutionOutcome
{
    Completed,
    StaleBaseline,
    PreconditionFailed,
    StateUncertain,
}
