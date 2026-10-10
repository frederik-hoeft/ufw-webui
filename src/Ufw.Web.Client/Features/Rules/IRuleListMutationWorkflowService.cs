using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;

namespace Ufw.Web.Client.Features.Rules;

internal interface IRuleListMutationWorkflowService
{
    Task<RuleInventoryState> UpdateMetadataAsync(RuleInventoryState state, string ruleId, RuleMetadataChange change, CancellationToken cancellationToken = default);

    Task<RuleGroup?> GetSingleRuleCleanupCandidateAsync(ListedFirewallRule rule, RuleSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<RuleListDeletionResult> DeleteAsync(ListedFirewallRule rule, string privateKey, RuleGroup? groupToDelete, CancellationToken cancellationToken = default);

    Task<RuleListOrderingResult> ApplyOrderingAsync(RuleInventoryState state, RuleOrderingPreview preview, string privateKey, CancellationToken cancellationToken = default);
}
