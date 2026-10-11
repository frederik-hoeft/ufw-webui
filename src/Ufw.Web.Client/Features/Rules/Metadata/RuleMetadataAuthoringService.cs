using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed class RuleMetadataAuthoringService(
    IRuleTagCatalogService tagCatalog,
    IRuleGroupCatalogService groupCatalog,
    IRuleTagColorGenerator tagColors) : IRuleMetadataAuthoringService
{
    public IReadOnlyList<RuleTag> Tags => tagCatalog.Current;

    public IReadOnlyList<RuleGroup> Groups => groupCatalog.Current;

    public async Task RefreshAsync(CancellationToken cancellationToken = default) =>
        await Task.WhenAll(tagCatalog.RefreshAsync(cancellationToken), groupCatalog.RefreshAsync(cancellationToken));

    public Task<IReadOnlyList<RuleTag>> RefreshTagsAsync(CancellationToken cancellationToken = default) => tagCatalog.RefreshAsync(cancellationToken);

    public MetadataCatalogSearchResult<RuleTag> SearchTags(string? term, IReadOnlyCollection<Guid> selectedIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);
        cancellationToken.ThrowIfCancellationRequested();
        string query = term?.Trim() ?? string.Empty;
        HashSet<Guid> selected = [.. selectedIds];
        RuleTag[] matches = [.. Tags.Where(tag => !selected.Contains(tag.Id) && (query.Length == 0 || tag.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)))];
        string? createName = CanCreate(query, RuleTagLimits.MAX_NAME_LENGTH, Tags.Select(static tag => tag.Name)) ? query : null;
        return new MetadataCatalogSearchResult<RuleTag>(matches, createName);
    }

    public MetadataCatalogSearchResult<RuleGroup> SearchGroups(string? term, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string query = term?.Trim() ?? string.Empty;
        RuleGroup[] matches = [.. Groups.Where(group => query.Length == 0 || group.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))];
        string? createName = CanCreate(query, RuleGroupLimits.MAX_NAME_LENGTH, Groups.Select(static group => group.Name)) ? query : null;
        return new MetadataCatalogSearchResult<RuleGroup>(matches, createName);
    }

    public IReadOnlyList<RuleTag> SelectTags(IReadOnlyCollection<Guid> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(selectedIds);
        HashSet<Guid> selected = [.. selectedIds];
        return [.. Tags.Where(tag => selected.Contains(tag.Id)).OrderBy(static tag => tag.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public RuleGroup? FindGroup(Guid groupId) => Groups.FirstOrDefault(group => group.Id == groupId);

    public string GenerateTagColor() => tagColors.Generate();

    public async Task<RuleTag> CreateTagAsync(string name, string? color = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = ValidateName(name, RuleTagLimits.MAX_NAME_LENGTH);
        string resolvedColor = color ?? tagColors.Generate();
        IReadOnlyList<RuleTag> tags = await tagCatalog.CreateAsync(normalizedName, resolvedColor, cancellationToken);
        return tags.SingleOrDefault(tag => string.Equals(tag.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ApiProtocolException("The created rule tag was not present in the authoritative catalog response.");
    }

    public Task<IReadOnlyList<RuleTag>> UpdateTagAsync(Guid id, string name, string color, CancellationToken cancellationToken = default) =>
        tagCatalog.UpdateAsync(id, ValidateName(name, RuleTagLimits.MAX_NAME_LENGTH), color, cancellationToken);

    public Task<IReadOnlyList<RuleTag>> DeleteTagAsync(Guid id, CancellationToken cancellationToken = default) => tagCatalog.DeleteAsync(id, cancellationToken);

    public async Task<RuleGroup> CreateGroupAsync(string name, string? comment = null, CancellationToken cancellationToken = default)
    {
        string normalizedName = ValidateName(name, RuleGroupLimits.MAX_NAME_LENGTH);
        IReadOnlyList<RuleGroup> groups = await groupCatalog.CreateAsync(normalizedName, comment, cancellationToken);
        return groups.SingleOrDefault(group => string.Equals(group.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ApiProtocolException("The created rule group was not present in the authoritative catalog response.");
    }

    public Task<IReadOnlyList<RuleGroup>> UpdateGroupAsync(Guid id, string name, string? comment, CancellationToken cancellationToken = default) =>
        groupCatalog.UpdateAsync(id, ValidateName(name, RuleGroupLimits.MAX_NAME_LENGTH), comment, cancellationToken);

    private static bool CanCreate(string name, int maxLength, IEnumerable<string> names) =>
        name.Length is > 0 && name.Length <= maxLength && !names.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));

    private static string ValidateName(string name, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(name);
        string normalized = name.Trim();
        if (normalized.Length == 0 || normalized.Length > maxLength)
        {
            throw new ArgumentException("Catalog name must be non-empty and within its maximum length.", nameof(name));
        }
        return normalized;
    }
}
