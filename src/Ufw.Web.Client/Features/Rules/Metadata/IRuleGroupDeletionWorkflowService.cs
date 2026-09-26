using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal interface IRuleGroupDeletionWorkflowService
{
    Task<RuleGroup?> GetSingleRuleCleanupCandidateAsync(ListedFirewallRule rule, RuleSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<RuleGroupDeletionWorkflowResult> DeleteAsync(
        RuleGroupManagementProjection projection,
        RuleSnapshot? snapshot,
        string? privateKey,
        CancellationToken cancellationToken = default);

    Task<RuleGroupCleanupResult> DeleteIfEmptyAsync(Guid groupId, CancellationToken cancellationToken = default);
}
