namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public enum RuleBatchDeleteOperationOutcome
{
    Deleted,
    DeletedAfterProcessFailure,
    Failed,
    StateUncertain,
}
