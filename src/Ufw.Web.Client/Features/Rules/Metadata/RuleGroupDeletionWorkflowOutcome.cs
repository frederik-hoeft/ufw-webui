namespace Ufw.Web.Client.Features.Rules.Metadata;

internal enum RuleGroupDeletionWorkflowOutcome
{
    Deleted,
    GroupChanged,
    GroupRetained,
    GroupCleanupFailed,
    BatchIncomplete,
}
