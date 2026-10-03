using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules;
using Ufw.Web.Data.Access.Rules.Metadata;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Rules;

internal sealed partial class RuleMetadataService(
    IRuleDaemonGateway daemonRules,
    IRuleMetadataDataAccess metadata,
    IRuleMetadataValuesNormalizer metadataNormalizer,
    ILogger<RuleMetadataService> logger) : IRuleMetadataService
{
    public async Task<RuleMetadataUpdateResult> UpdateAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(request);
        if (!metadataNormalizer.TryNormalize(request.Notes, request.TagIds, request.GroupId, out RuleMetadataValues? values))
        {
            return new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.InvalidMetadata);
        }

        DaemonResult<RuleListResponse> daemonResult = await daemonRules.GetRulesAsync(cancellationToken);
        RuleListResponse snapshot = daemonResult.Result;
        LiveRuleIdentitySet liveRuleIds = LiveRuleIdentitySet.FromSnapshot(snapshot);
        bool exists = liveRuleIds.Contains(ruleId);
        if (!exists)
        {
            return new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.RuleNotFound);
        }

        DataMutationResult<RuleMetadataItem?> save = await metadata.SaveAsync(ruleId, values, cancellationToken);
        if (save.IsSuccess)
        {
            return new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.Success, new RuleMetadataMutationResponse { Metadata = save.Value });
        }

        return save.Error switch
        {
            RuleTagsNotFoundError => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.TagNotFound),
            RuleGroupNotFoundError => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.GroupNotFound),
            DataMutationReferenceConflictError => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.InvalidMetadata),
            _ => throw new InvalidOperationException($"Unknown metadata mutation error '{save.Error!.GetType().Name}'."),
        };
    }

    public async Task<RuleReplacementMetadataReconciliationOutcome> ReconcileReplacementAsync(
        RuleReplacementReconciliationFacts facts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facts);
        try
        {
            _ = await metadata.ReconcileReplacementAsync(facts.OriginalRuleId, facts.ReplacementRuleId, facts.OriginalRuleStillLive, cancellationToken);
            return RuleReplacementMetadataReconciliationOutcome.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogReplacementReconciliationFailure(logger, facts.OriginalRuleId, facts.ReplacementRuleId, exception);
            return RuleReplacementMetadataReconciliationOutcome.Failed;
        }
    }

    public async Task RemoveForDeletedRuleAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        try
        {
            _ = await metadata.DeleteAsync(ruleId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogCleanupFailure(logger, ruleId, exception);
        }
    }

    public async Task ReconcileBatchDeleteAsync(RuleBatchDeleteResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.FinalSnapshot is null)
        {
            return;
        }

        LiveRuleIdentitySet liveRuleIds = LiveRuleIdentitySet.FromSnapshot(response.FinalSnapshot);
        string[] confirmedDeletedRuleIds = response.Operations
            .Where(static operation => operation.Outcome is RuleBatchDeleteOperationOutcome.Deleted or RuleBatchDeleteOperationOutcome.DeletedAfterProcessFailure)
            .Select(static operation => operation.RuleId)
            .Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId))
            .Select(static ruleId => ruleId!)
            .Distinct(StringComparer.Ordinal)
            .Where(ruleId => !liveRuleIds.Contains(ruleId))
            .ToArray();
        if (confirmedDeletedRuleIds.Length == 0)
        {
            return;
        }

        try
        {
            _ = await metadata.DeleteForRuleIdsAsync(confirmedDeletedRuleIds, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogBatchCleanupFailure(logger, confirmedDeletedRuleIds.Length, exception);
        }
    }

    [LoggerMessage(1, LogLevel.Warning, "Rule metadata cleanup failed after deleting firewall rule {RuleId}.")]
    private static partial void LogCleanupFailure(ILogger logger, string ruleId, Exception exception);

    [LoggerMessage(2, LogLevel.Warning, "Rule metadata cleanup failed after batch deletion for {RuleCount} semantic rule identities.")]
    private static partial void LogBatchCleanupFailure(ILogger logger, int ruleCount, Exception exception);

    [LoggerMessage(3, LogLevel.Error, "Rule metadata reconciliation failed after replacing firewall rule {OriginalRuleId} with {ReplacementRuleId}.")]
    private static partial void LogReplacementReconciliationFailure(ILogger logger, string originalRuleId, string replacementRuleId, Exception exception);
}
