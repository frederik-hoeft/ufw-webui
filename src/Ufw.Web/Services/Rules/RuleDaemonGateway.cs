using System.Text.Json;
using Ufw.Ipc.Client;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Rules;

internal sealed partial class RuleDaemonGateway(IUfwClient ufwClient, ILogger<RuleDaemonGateway> logger) : IRuleDaemonGateway
{
    private const string RULES_ROUTE = "/api/v1/rules";

    public Task<DaemonResult<RuleListResponse>> GetRulesAsync(CancellationToken cancellationToken = default) =>
        DaemonResult.FromIpcAsync(() => ufwClient.TrySendAsync<RuleListResponse>(RequestMethod.Get, RULES_ROUTE, cancellationToken));

    public Task<DaemonResult<RuleMutationResponse>> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.FromIpcAsync(() => ufwClient.TrySendAsync<AddRuleRequest, RuleMutationResponse>(request, cancellationToken));

    public Task<DaemonResult<RuleInsertionResponse>> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.FromIpcAsync(() => ufwClient.TrySendAsync<InsertRuleRequest, RuleInsertionResponse>(request, cancellationToken));

    public async Task<DaemonResult<RuleReplacementExecutionResult>> ReplaceRuleAsync(ReplaceRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DaemonResult<RuleReplacementResponse> daemonResult = await DaemonResult.FromIpcAsync(
            () => ufwClient.TrySendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, cancellationToken));
        if (!daemonResult.TryGetResult(out RuleReplacementResponse? response, out UfwIpcError? error))
        {
            return DaemonResult.Failure<RuleReplacementExecutionResult>(error);
        }

        RuleReplacementReconciliationPlan reconciliation = PrepareReplacementReconciliation(request, response);
        return DaemonResult.Success(new RuleReplacementExecutionResult(response, reconciliation));
    }

    public Task<DaemonResult<RuleReorderResponse>> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.FromIpcAsync(() => ufwClient.TrySendAsync<ReorderRulesRequest, RuleReorderResponse>(request, cancellationToken));

    public Task<DaemonResult<RuleBatchDeleteResponse>> BatchDeleteRulesAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.FromIpcAsync(() => ufwClient.TrySendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken));

    public Task<DaemonResult<RuleMutationResponse>> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default) =>
        DaemonResult.FromIpcAsync(() => ufwClient.TrySendAsync<DeleteRuleRequest, RuleMutationResponse>(request, cancellationToken));

    private RuleReplacementReconciliationPlan PrepareReplacementReconciliation(ReplaceRuleRequest request, RuleReplacementResponse response)
    {
        if (response.Outcome != RuleReplacementOutcome.Completed)
        {
            return new RuleReplacementReconciliationNotRequired();
        }

        string? originalRuleId = null;
        string replacementRuleId = response.ReplacementRule?.RuleId ?? string.Empty;
        try
        {
            ReplaceRulePayload payload = JsonSerializer.Deserialize(request.Payload, MessageJsonSerializerContext.Default.ReplaceRulePayload)
                ?? throw new InvalidDataException("Completed rule replacement request did not contain a replacement payload.");
            RuleReplacementContract.ValidatePayload(payload);
            originalRuleId = payload.OriginalRuleId;

            if (response.FinalSnapshot is null || !RuleIdentity.IsValid(replacementRuleId))
            {
                throw new InvalidDataException("Completed rule replacement response did not contain an authoritative replacement identity and final snapshot.");
            }
            if (!string.Equals(RuleIdentity.Compute(payload.ReplacementRule), replacementRuleId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Completed rule replacement response identity does not match the signed replacement rule.");
            }
            if (!response.FinalSnapshot.Rules.Any(rule => string.Equals(rule.RuleId, replacementRuleId, StringComparison.Ordinal)))
            {
                throw new InvalidDataException("Completed rule replacement response does not contain the confirmed replacement identity in its final snapshot.");
            }

            bool originalRuleStillLive = response.FinalSnapshot.Rules.Any(rule => string.Equals(rule.RuleId, originalRuleId, StringComparison.Ordinal));
            RuleReplacementReconciliationFacts facts = new(originalRuleId, replacementRuleId, originalRuleStillLive);
            return new RuleReplacementReconciliationReady(facts);
        }
        catch (Exception exception)
        {
            LogReplacementInterpretationFailure(
                logger,
                originalRuleId ?? "<unknown>",
                string.IsNullOrWhiteSpace(replacementRuleId) ? "<unknown>" : replacementRuleId,
                exception);
            return new RuleReplacementReconciliationPreparationFailed();
        }
    }

    [LoggerMessage(1, LogLevel.Error, "Rule replacement result could not be interpreted for metadata reconciliation from rule {OriginalRuleId} to {ReplacementRuleId}.")]
    private static partial void LogReplacementInterpretationFailure(ILogger logger, string originalRuleId, string replacementRuleId, Exception exception);
}
