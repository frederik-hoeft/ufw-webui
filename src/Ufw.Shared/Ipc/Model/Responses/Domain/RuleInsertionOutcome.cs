namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public enum RuleInsertionOutcome
{
    Completed,
    StaleBaseline,
    PreconditionFailed,
    StateUncertain,
}
