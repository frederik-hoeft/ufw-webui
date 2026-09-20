namespace Ufw.Client.Rules;

internal enum RuleSnapshotStaleReason
{
    RefreshFailed,
    MutationCommitted,
    MutationOutcomeUnknown,
    MutationRejectedRequiresRefresh,
}
