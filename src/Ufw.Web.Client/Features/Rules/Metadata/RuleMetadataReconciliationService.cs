using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.RuleMetadata;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed class RuleMetadataReconciliationService(IRuleMetadataReconciliationApiClient apiClient)
    : IRuleMetadataReconciliationService
{
    public async Task<RuleMetadataReconciliationSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        RuleMetadataReconciliationResponse response = await apiClient.GetAsync(cancellationToken);
        return Normalize(response);
    }

    public async Task<RuleMetadataReconciliationSnapshot> CleanupAsync(IReadOnlyCollection<Guid> metadataIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadataIds);
        if (metadataIds.Count == 0 || metadataIds.Any(static id => id == Guid.Empty))
        {
            throw new ArgumentException("At least one valid metadata identity is required.", nameof(metadataIds));
        }

        Guid[] selectedIds = [.. metadataIds.Distinct().Order()];

        CleanupRuleMetadataRequest request = new() { MetadataIds = selectedIds };
        RuleMetadataReconciliationResponse response = await apiClient.CleanupAsync(request, cancellationToken);
        return Normalize(response);
    }

    private static RuleMetadataReconciliationSnapshot Normalize(RuleMetadataReconciliationResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Orphans is null || response.RemovedCount < 0)
        {
            throw new ApiProtocolException("Rule-metadata reconciliation response is invalid.");
        }

        List<OrphanedRuleMetadata> orphans = new(response.Orphans.Count);
        HashSet<Guid> metadataIds = [];
        HashSet<string> ruleIds = new(StringComparer.Ordinal);
        foreach (RuleMetadataItem item in response.Orphans)
        {
            if (item is null
                || item.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(item.RuleId)
                || item.Tags is null
                || !metadataIds.Add(item.Id)
                || !ruleIds.Add(item.RuleId))
            {
                throw new ApiProtocolException("Rule-metadata reconciliation response contains an invalid or duplicate orphan.");
            }

            RuleMetadata metadata = RuleMetadataProtocolMapper.MapMetadata(item, "Rule-metadata reconciliation response contains invalid metadata.");
            orphans.Add(new OrphanedRuleMetadata(item.RuleId, metadata));
        }

        return new RuleMetadataReconciliationSnapshot(
            orphans.OrderBy(static item => item.RuleId, StringComparer.Ordinal).ToArray(),
            response.RemovedCount);
    }
}
