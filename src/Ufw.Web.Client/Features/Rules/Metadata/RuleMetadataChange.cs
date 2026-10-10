namespace Ufw.Web.Client.Features.Rules.Metadata;

/// <summary>
/// Editable rule metadata without HTTP transport or UI-component dependencies.
/// </summary>
internal sealed record RuleMetadataChange(string? Notes, IReadOnlyList<Guid> TagIds, Guid? GroupId)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Notes) && TagIds.Count == 0 && GroupId is null;

    public bool HasSameValueAs(RuleMetadataChange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(NormalizeNotes(Notes), NormalizeNotes(other.Notes), StringComparison.Ordinal)
            && GroupId == other.GroupId
            && TagIds.Distinct().Order().SequenceEqual(other.TagIds.Distinct().Order());
    }

    private static string? NormalizeNotes(string? notes) => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
}
