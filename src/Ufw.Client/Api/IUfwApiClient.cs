using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Responses.Domain;

namespace Ufw.Client.Api;

public interface IUfwApiClient
{
    Task<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> AddRuleAsync(
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> DeleteRuleAsync(
        ListedFirewallRule rule,
        string privateKey,
        CancellationToken cancellationToken = default);
}
