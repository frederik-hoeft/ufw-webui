using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed record RuleMetadataEditorResult(string? Notes, IReadOnlyList<Guid> TagIds, Guid? GroupId)
{
    public static RuleMetadataEditorResult Empty { get; } = new(null, [], null);

    public static RuleMetadataEditorResult FromMetadata(RuleMetadata? metadata) => metadata is null
        ? Empty
        : new RuleMetadataEditorResult(metadata.Notes, metadata.Tags.Select(static tag => tag.Id).ToArray(), metadata.Group?.Id);

    public RuleMetadataEditorResult Normalize() => new(string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(), TagIds.Distinct().Order().ToArray(), GroupId);

    internal bool HasSameValueAs(RuleMetadataEditorResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new RuleMetadataChange(Notes, TagIds, GroupId).HasSameValueAs(new RuleMetadataChange(other.Notes, other.TagIds, other.GroupId));
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Notes) && TagIds.Count == 0 && GroupId is null;
}
