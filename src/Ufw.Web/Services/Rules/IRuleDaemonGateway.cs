using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Services.Rules;

/// <summary>
/// Provides the Web application's daemon-facing rule capabilities while keeping IPC route and transport details out of higher-level rule workflows.
/// </summary>
public interface IRuleDaemonGateway
{
    Task<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default);

    Task<RuleInsertionResponse> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken = default);

    Task<RuleReplacementResponse> ReplaceRuleAsync(ReplaceRuleRequest request, CancellationToken cancellationToken = default);

    Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default);

    Task<RuleBatchDeleteResponse> BatchDeleteRulesAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken = default);

    Task<RuleMutationResponse> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default);
}
