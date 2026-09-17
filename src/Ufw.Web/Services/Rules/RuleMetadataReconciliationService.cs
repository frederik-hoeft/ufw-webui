using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleMetadataReconciliationService(
    IDaemonRuleSource daemonRules,
    IRuleMetadataRepository repository) : IRuleMetadataReconciliationService
{
    public async Task<RuleMetadataReconciliationResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        RuleListResponse snapshot = await daemonRules.GetAsync(cancellationToken);
        IReadOnlyList<RuleMetadataItem> metadata = await repository.GetAllAsync(cancellationToken);
        return BuildResponse(snapshot, metadata, removedCount: 0);
    }

    public async Task<RuleMetadataReconciliationResponse> CleanupAsync(
        CleanupRuleMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MetadataIds is null || request.MetadataIds.Count == 0 || request.MetadataIds.Any(static id => id == Guid.Empty))
        {
            throw new ArgumentException("At least one valid metadata identity is required.", nameof(request));
        }

        Guid[] selectedIds = [.. request.MetadataIds.Distinct().Order()];
        RuleListResponse snapshot = await daemonRules.GetAsync(cancellationToken);
        string[] liveRuleIds = GetLiveRuleIds(snapshot);
        int removedCount = await repository.DeleteUnmatchedAsync(selectedIds, liveRuleIds, cancellationToken);
        IReadOnlyList<RuleMetadataItem> metadata = await repository.GetAllAsync(cancellationToken);
        return BuildResponse(snapshot, metadata, removedCount);
    }

    private static RuleMetadataReconciliationResponse BuildResponse(
        RuleListResponse snapshot,
        IReadOnlyList<RuleMetadataItem> metadata,
        int removedCount)
    {
        HashSet<string> liveRuleIds = GetLiveRuleIds(snapshot).ToHashSet(StringComparer.Ordinal);
        RuleMetadataItem[] orphans = [.. metadata
            .Where(item => !liveRuleIds.Contains(item.RuleId))
            .OrderBy(static item => item.RuleId, StringComparer.Ordinal)
            .ThenBy(static item => item.Id)];
        return new RuleMetadataReconciliationResponse(orphans, removedCount);
    }

    private static string[] GetLiveRuleIds(RuleListResponse snapshot) => [.. snapshot.Rules
        .Select(static rule => rule.RuleId)
        .Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId))
        .Cast<string>()
        .Distinct(StringComparer.Ordinal)];
}
