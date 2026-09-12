namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public enum RuleReorderOutcome
{
    Completed,
    StaleBaseline,
    PreconditionFailed,
    PartiallyCompleted,
    RecoveryFailed,
    StateUncertain,
}
