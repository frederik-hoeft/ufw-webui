using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Rules;

/// <summary>
/// Provides the Web application's daemon-facing rule capabilities while keeping IPC route and transport details out of higher-level rule workflows.
/// </summary>
public interface IRuleDaemonGateway
{
    Task<DaemonResult<RuleListResponse>> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<DaemonResult<RuleMutationResponse>> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default);

    Task<DaemonResult<RuleInsertionResponse>> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken = default);

    Task<DaemonResult<RuleReplacementResponse>> ReplaceRuleAsync(ReplaceRuleRequest request, CancellationToken cancellationToken = default);

    Task<DaemonResult<RuleReorderResponse>> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default);

    Task<DaemonResult<RuleBatchDeleteResponse>> BatchDeleteRulesAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken = default);

    Task<DaemonResult<RuleMutationResponse>> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default);
}
