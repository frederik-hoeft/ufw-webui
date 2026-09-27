namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public enum RuleReplacementOutcome
{
    Completed,
    StaleBaseline,
    PreconditionFailed,
    PartiallyCompleted,
    StateUncertain,
}
