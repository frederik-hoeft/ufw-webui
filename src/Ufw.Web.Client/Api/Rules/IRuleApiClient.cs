using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api.Rules.Model;

namespace Ufw.Web.Client.Api.Rules;

internal interface IRuleApiClient
{
    Task<RuleInventoryResponse> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataMutationResponse> UpdateMetadataAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default);

    Task<RuleInsertionResponse> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken = default);

    Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default);
}
