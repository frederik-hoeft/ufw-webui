using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleDaemonGateway(IUfwClient ufwClient) : IRuleDaemonGateway
{
    private const string RULES_ROUTE = "/api/v1/rules";

    public Task<DaemonResult<RuleListResponse>> GetRulesAsync(CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync<RuleListResponse>(RequestMethod.Get, RULES_ROUTE, cancellationToken));

    public Task<DaemonResult<RuleMutationResponse>> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync<AddRuleRequest, RuleMutationResponse>(request, cancellationToken));

    public Task<DaemonResult<RuleInsertionResponse>> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync<InsertRuleRequest, RuleInsertionResponse>(request, cancellationToken));

    public Task<DaemonResult<RuleReplacementResponse>> ReplaceRuleAsync(ReplaceRuleRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, cancellationToken));

    public Task<DaemonResult<RuleReorderResponse>> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync<ReorderRulesRequest, RuleReorderResponse>(request, cancellationToken));

    public Task<DaemonResult<RuleBatchDeleteResponse>> BatchDeleteRulesAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken));

    public Task<DaemonResult<RuleMutationResponse>> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync<DeleteRuleRequest, RuleMutationResponse>(request, cancellationToken));
}
