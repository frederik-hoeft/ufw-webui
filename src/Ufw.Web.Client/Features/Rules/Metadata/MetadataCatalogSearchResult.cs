namespace Ufw.Web.Client.Features.Rules.Metadata;

public sealed record MetadataCatalogSearchResult<T>(IReadOnlyList<T> Matches, string? CreateName);
