using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;

namespace Ufw.Web.Client.Features.Rules.Metadata;

/// <summary>
/// Validates and maps the shared rule-metadata transport primitives to client-domain values.
/// Collection-specific uniqueness and ordering remain the caller's responsibility.
/// </summary>
internal static class RuleMetadataProtocolMapper
{
    public static RuleTag MapTag(RuleTagItem? item, string diagnostic)
    {
        if (item is null || item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) || !RuleTagColor.TryNormalize(item.Color, out string color))
        {
            throw new ApiProtocolException(diagnostic);
        }

        return new RuleTag(item.Id, item.Name.Trim(), color);
    }

    public static RuleGroupMembership? MapGroupReference(RuleGroupSummary? group, string diagnostic)
    {
        if (group is null)
        {
            return null;
        }
        if (group.Id == Guid.Empty || string.IsNullOrWhiteSpace(group.Name))
        {
            throw new ApiProtocolException(diagnostic);
        }

        return new RuleGroupMembership(group.Id, group.Name.Trim(), string.IsNullOrWhiteSpace(group.Comment) ? null : group.Comment.Trim());
    }

    public static RuleMetadata MapMetadata(RuleMetadataItem? item, string diagnostic)
    {
        if (item is null || item.Id == Guid.Empty || item.Tags is null)
        {
            throw new ApiProtocolException(diagnostic);
        }

        RuleTag[] tags = [.. item.Tags.Select(tag => MapTag(tag, diagnostic))];
        if (tags.Select(static tag => tag.Id).Distinct().Count() != tags.Length)
        {
            throw new ApiProtocolException(diagnostic);
        }

        return new RuleMetadata(item.Id, string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(), tags, MapGroupReference(item.Group, diagnostic));
    }
}
