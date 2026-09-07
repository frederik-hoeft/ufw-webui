using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Api;

public interface IUfwApiClient
{
    Task<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<IntentContextResponse> GetIntentContextAsync(CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> AddRuleAsync(
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> DeleteRuleAsync(
        ListedFirewallRule rule,
        string privateKey,
        CancellationToken cancellationToken = default);
}
