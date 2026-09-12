using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Api;

internal interface IRuleApiClient
{
    Task<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default);

    Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default);
}
