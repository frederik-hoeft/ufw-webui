using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.Rules;

using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Rules;

internal sealed partial class RuleMetadataService(
    IRuleDaemonGateway daemonRules,
    IRuleMetadataRepository repository,
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
        bool exists = snapshot.Rules.Any(rule => string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal));
        if (!exists)
        {
            return new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.RuleNotFound);
        }

        RuleMetadataSaveResult save = await repository.SaveAsync(ruleId, values, cancellationToken);
        return save.Outcome switch
        {
            RuleMetadataSaveOutcome.Success => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.Success, new RuleMetadataMutationResponse { Metadata = save.Metadata }),
            RuleMetadataSaveOutcome.TagNotFound => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.TagNotFound),
            RuleMetadataSaveOutcome.GroupNotFound => new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.GroupNotFound),
            _ => throw new InvalidOperationException($"Unknown metadata save outcome '{save.Outcome}'."),
        };
    }

    public async Task<RuleReplacementMetadataReconciliationOutcome> ReconcileReplacementAsync(
        ReplaceRuleRequest request,
        RuleReplacementResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        if (response.Outcome != RuleReplacementOutcome.Completed)
        {
            return RuleReplacementMetadataReconciliationOutcome.NotAttempted;
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
            _ = await repository.ReconcileReplacementAsync(originalRuleId, replacementRuleId, originalRuleStillLive, cancellationToken);
            return RuleReplacementMetadataReconciliationOutcome.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogReplacementReconciliationFailure(logger, originalRuleId ?? "<unknown>", string.IsNullOrWhiteSpace(replacementRuleId) ? "<unknown>" : replacementRuleId, exception);
            return RuleReplacementMetadataReconciliationOutcome.Failed;
        }
    }

    public async Task RemoveForDeletedRuleAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        try
        {
            _ = await repository.DeleteAsync(ruleId, cancellationToken);
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

        HashSet<string> liveRuleIds = response.FinalSnapshot.Rules
            .Select(static rule => rule.RuleId)
            .Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId))
            .Select(static ruleId => ruleId!)
            .ToHashSet(StringComparer.Ordinal);
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
            _ = await repository.DeleteForRuleIdsAsync(confirmedDeletedRuleIds, cancellationToken);
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
