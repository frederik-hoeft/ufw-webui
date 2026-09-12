namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public enum RuleReorderOperationOutcome
{
    Applied,
    AppliedAfterProcessFailure,
    FailedAndRestored,
    PresenceConfirmedAfterInterruption,
    RecoveryFailed,
}
