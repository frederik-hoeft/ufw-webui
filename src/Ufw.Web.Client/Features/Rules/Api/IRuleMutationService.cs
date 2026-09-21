using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules.Api;

internal interface IRuleMutationService
{
    Task<RuleMutationResponse> AddRuleAsync(FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> DeleteRuleAsync(ListedFirewallRule rule, string privateKey, CancellationToken cancellationToken = default);

    Task<RuleInsertionResponse> InsertRuleAsync(
        RuleListResponse baseline,
        int anchorOccurrenceId,
        RuleInsertionPlacement placement,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default);
}
