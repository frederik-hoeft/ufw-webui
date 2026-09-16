namespace Ufw.Client.Components.Rules.Metadata;

internal sealed record RuleMetadataEditorResult(string? Notes, IReadOnlyList<Guid> TagIds);
