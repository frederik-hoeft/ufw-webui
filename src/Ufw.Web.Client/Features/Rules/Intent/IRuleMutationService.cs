using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules.Intent;

internal interface IRuleMutationService
{
    Task<RuleMutationResponse> AddRuleAsync(FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> DeleteRuleAsync(ListedFirewallRule rule, string privateKey, CancellationToken cancellationToken = default);

    Task<RuleBatchDeleteResponse> BatchDeleteRulesAsync(
        RuleListResponse baseline,
        IReadOnlyList<int> occurrenceIds,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<RuleInsertionResponse> InsertRuleAsync(
        RuleListResponse baseline,
        int anchorOccurrenceId,
        RuleInsertionPlacement placement,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<RuleReplacementMutationResponse> ReplaceRuleAsync(
        RuleListResponse baseline,
        int targetOccurrenceId,
        string originalRuleId,
        FirewallRuleSpecification replacementRule,
        string privateKey,
        CancellationToken cancellationToken = default);
}
