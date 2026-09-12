namespace Ufw.Systemd.Firewall.Ordering;

internal enum RuleReorderOperationStatus
{
    Applied,
    AppliedAfterProcessFailure,
    FailedAndRestored,
    PresenceConfirmedAfterInterruption,
    RecoveryFailed,
}
