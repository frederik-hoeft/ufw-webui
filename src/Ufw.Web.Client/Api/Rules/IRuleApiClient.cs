using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Client.Api.Rules;

internal interface IRuleApiClient
{
    Task<RuleInventoryResponse> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataMutationResponse> UpdateMetadataAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> AddRuleAsync(AddRuleIntentRequest request, CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> DeleteRuleAsync(DeleteRuleIntentRequest request, CancellationToken cancellationToken = default);

    Task<RuleBatchDeleteResponse> BatchDeleteRulesAsync(BatchDeleteRulesIntentRequest request, CancellationToken cancellationToken = default);

    Task<RuleInsertionResponse> InsertRuleAsync(InsertRuleIntentRequest request, CancellationToken cancellationToken = default);

    Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesIntentRequest request, CancellationToken cancellationToken = default);

    Task<RuleReplacementMutationResponse> ReplaceRuleAsync(ReplaceRuleIntentRequest request, CancellationToken cancellationToken = default);
}
