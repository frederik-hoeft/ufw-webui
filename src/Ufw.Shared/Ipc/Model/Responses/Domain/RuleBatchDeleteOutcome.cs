namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public enum RuleBatchDeleteOutcome
{
    Completed,
    StaleBaseline,
    PreconditionFailed,
    PartiallyCompleted,
    StateUncertain,
}
