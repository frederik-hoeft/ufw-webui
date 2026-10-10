namespace Ufw.Web.Client.Features.Rules.Metadata;

public interface IRuleMetadataAuthoringService
{
    IReadOnlyList<RuleTag> Tags { get; }

    IReadOnlyList<RuleGroup> Groups { get; }

    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTag>> RefreshTagsAsync(CancellationToken cancellationToken = default);

    MetadataCatalogSearchResult<RuleTag> SearchTags(string? term, IReadOnlyCollection<Guid> selectedIds, CancellationToken cancellationToken = default);

    MetadataCatalogSearchResult<RuleGroup> SearchGroups(string? term, CancellationToken cancellationToken = default);

    IReadOnlyList<RuleTag> SelectTags(IReadOnlyCollection<Guid> selectedIds);

    RuleGroup? FindGroup(Guid groupId);

    string GenerateTagColor();

    Task<RuleTag> CreateTagAsync(string name, string? color = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTag>> UpdateTagAsync(Guid id, string name, string color, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleTag>> DeleteTagAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RuleGroup> CreateGroupAsync(string name, string? comment = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleGroup>> UpdateGroupAsync(Guid id, string name, string? comment, CancellationToken cancellationToken = default);
}
