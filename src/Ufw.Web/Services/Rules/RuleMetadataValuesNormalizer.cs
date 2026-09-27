using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleMetadataValuesNormalizer : IRuleMetadataValuesNormalizer
{
    private const int MAX_TAG_COUNT = 32;

    public bool TryNormalize(string? notes, IReadOnlyList<Guid>? tagIds, Guid? groupId, [NotNullWhen(true)] out RuleMetadataValues? values)
    {
        string? normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalizedNotes?.Length > RuleMetadataEntry.MAX_NOTES_LENGTH
            || tagIds is null
            || tagIds.Count > MAX_TAG_COUNT
            || tagIds.Any(static id => id == Guid.Empty)
            || groupId == Guid.Empty)
        {
            values = null;
            return false;
        }

        Guid[] normalizedTagIds = [.. tagIds.Distinct().Order()];
        values = new RuleMetadataValues(normalizedNotes, normalizedTagIds, groupId);
        return true;
    }
}
