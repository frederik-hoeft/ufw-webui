using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Components.Rules.Metadata;

public sealed record RuleMetadataEditorResult(string? Notes, IReadOnlyList<Guid> TagIds)
{
    public static RuleMetadataEditorResult Empty { get; } = new(null, []);

    public static RuleMetadataEditorResult FromMetadata(RuleMetadata? metadata) => metadata is null
        ? Empty
        : new RuleMetadataEditorResult(metadata.Notes, metadata.Tags.Select(static tag => tag.Id).ToArray());

    public RuleMetadataEditorResult Normalize() => new(string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(), TagIds.Distinct().Order().ToArray());

    public bool IsEmpty => string.IsNullOrWhiteSpace(Notes) && TagIds.Count == 0;
}
