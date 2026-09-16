using Ufw.Client.Api;

namespace Ufw.Client.Rules.Metadata;

internal sealed class RuleTagCatalogService(IRuleTagApiClient apiClient) : IRuleTagCatalogService
{
    public IReadOnlyList<RuleTag> Current { get; private set; } = [];

    public long Version { get; private set; }

    public async Task<IReadOnlyList<RuleTag>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.GetAsync(cancellationToken));
        return Current;
    }

    public async Task<IReadOnlyList<RuleTag>> CreateAsync(string name, string color, CancellationToken cancellationToken = default)
    {
        RuleTagInventoryResponse response = await apiClient.CreateAsync(new CreateRuleTagRequest
        {
            Name = name,
            Color = color,
        }, cancellationToken);
        Current = Normalize(response);
        Version++;
        return Current;
    }

    public async Task<IReadOnlyList<RuleTag>> UpdateAsync(Guid tagId, string name, string color, CancellationToken cancellationToken = default)
    {
        RuleTagInventoryResponse response = await apiClient.UpdateAsync(tagId, new UpdateRuleTagRequest
        {
            Name = name,
            Color = color,
        }, cancellationToken);
        Current = Normalize(response);
        Version++;
        return Current;
    }

    public async Task<IReadOnlyList<RuleTag>> DeleteAsync(Guid tagId, CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.DeleteAsync(tagId, cancellationToken));
        Version++;
        return Current;
    }

    private static IReadOnlyList<RuleTag> Normalize(RuleTagInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Tags is null)
        {
            throw new ApiProtocolException("Rule-tag inventory response is missing the tag list.");
        }

        List<RuleTag> tags = new(response.Tags.Count);
        foreach (RuleTagItem item in response.Tags)
        {
            if (item is null
                || item.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(item.Name)
                || !RuleTagColor.TryNormalize(item.Color, out string color))
            {
                throw new ApiProtocolException("Rule-tag inventory response contains an invalid tag entry.");
            }

            tags.Add(new RuleTag(item.Id, item.Name.Trim(), color));
        }

        if (tags.Select(static tag => tag.Id).Distinct().Count() != tags.Count
            || tags.Select(static tag => tag.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != tags.Count)
        {
            throw new ApiProtocolException("Rule-tag inventory response contains duplicate tag identities.");
        }

        return tags
            .OrderBy(static tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static tag => tag.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
