using Ufw.Web.Client.Api.RuleMetadata;
using Ufw.Web.Client.Api.RuleMetadata.Model;
using Ufw.Web.Client.Api.RuleTags.Model;
using Ufw.Web.Client.Api.Rules.Model;
using Ufw.Web.Client.Api;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed class RuleMetadataReconciliationService(IRuleMetadataReconciliationApiClient apiClient)
    : IRuleMetadataReconciliationService
{
    public async Task<RuleMetadataReconciliationSnapshot> RefreshAsync(CancellationToken cancellationToken = default) =>
        Normalize(await apiClient.GetAsync(cancellationToken));

    public async Task<RuleMetadataReconciliationSnapshot> CleanupAsync(IReadOnlyCollection<Guid> metadataIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadataIds);
        if (metadataIds.Count == 0 || metadataIds.Any(static id => id == Guid.Empty))
        {
            throw new ArgumentException("At least one valid metadata identity is required.", nameof(metadataIds));
        }

        Guid[] selectedIds = [.. metadataIds.Distinct().Order()];

        return Normalize(await apiClient.CleanupAsync(new CleanupRuleMetadataRequest { MetadataIds = selectedIds }, cancellationToken));
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

            List<RuleTag> tags = new(item.Tags.Count);
            HashSet<Guid> tagIds = [];
            foreach (RuleTagItem tag in item.Tags)
            {
                if (tag is null
                    || tag.Id == Guid.Empty
                    || string.IsNullOrWhiteSpace(tag.Name)
                    || !RuleTagColor.TryNormalize(tag.Color, out string color)
                    || !tagIds.Add(tag.Id))
                {
                    throw new ApiProtocolException("Rule-metadata reconciliation response contains invalid tag data.");
                }
                tags.Add(new RuleTag(tag.Id, tag.Name.Trim(), color));
            }

            orphans.Add(new OrphanedRuleMetadata(item.RuleId, new RuleMetadata(item.Id, string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(), tags)));
        }

        return new RuleMetadataReconciliationSnapshot(
            orphans.OrderBy(static item => item.RuleId, StringComparer.Ordinal).ToArray(),
            response.RemovedCount);
    }
}
