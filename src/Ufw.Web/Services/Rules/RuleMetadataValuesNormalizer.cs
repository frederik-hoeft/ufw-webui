using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleMetadataValuesNormalizer : IRuleMetadataValuesNormalizer
{
    public bool TryNormalize(string? notes, IReadOnlyList<Guid>? tagIds, Guid? groupId, [NotNullWhen(true)] out RuleMetadataValues? values)
    {
        if (tagIds is null
            || tagIds.Count > RuleMetadataLimits.MAX_TAG_COUNT
            || tagIds.Any(static id => id == Guid.Empty)
            || groupId == Guid.Empty)
        {
            values = null;
            return false;
        }

        RuleMetadataValues normalized = RuleMetadataValues.FromValidatedRequest(notes, tagIds, groupId);
        if (normalized.Notes?.Length > RuleMetadataLimits.MAX_NOTES_LENGTH)
        {
            values = null;
            return false;
        }

        values = normalized;
        return true;
    }
}
