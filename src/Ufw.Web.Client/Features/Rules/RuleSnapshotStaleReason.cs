namespace Ufw.Web.Client.Features.Rules;

internal enum RuleSnapshotStaleReason
{
    RefreshFailed,
    MutationCommitted,
    MutationOutcomeUnknown,
    MutationRejectedRequiresRefresh,
}
