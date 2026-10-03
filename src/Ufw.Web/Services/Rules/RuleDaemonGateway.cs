using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleDaemonGateway(IUfwClient ufwClient) : IRuleDaemonGateway
{
    private const string RULES_ROUTE = "/api/v1/rules";

    public Task<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync<RuleListResponse>(RequestMethod.Get, RULES_ROUTE, cancellationToken);

    public Task<RuleMutationResponse> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync<AddRuleRequest, RuleMutationResponse>(request, cancellationToken);

    public Task<RuleInsertionResponse> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync<InsertRuleRequest, RuleInsertionResponse>(request, cancellationToken);

    public Task<RuleReplacementResponse> ReplaceRuleAsync(ReplaceRuleRequest request, CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, cancellationToken);

    public Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync<ReorderRulesRequest, RuleReorderResponse>(request, cancellationToken);

    public Task<RuleBatchDeleteResponse> BatchDeleteRulesAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken);

    public Task<RuleMutationResponse> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync<DeleteRuleRequest, RuleMutationResponse>(request, cancellationToken);
}
