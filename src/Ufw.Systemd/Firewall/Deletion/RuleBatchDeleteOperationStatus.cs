namespace Ufw.Systemd.Firewall.Deletion;

internal enum RuleBatchDeleteOperationStatus
{
    Deleted,
    DeletedAfterProcessFailure,
    Failed,
    StateUncertain,
}
