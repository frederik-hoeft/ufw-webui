namespace Ufw.Web.Client.Features.Rules.Metadata;

/// <summary>
/// Editable rule metadata without HTTP transport or UI-component dependencies.
/// </summary>
internal sealed record RuleMetadataChange(string? Notes, IReadOnlyList<Guid> TagIds, Guid? GroupId);
