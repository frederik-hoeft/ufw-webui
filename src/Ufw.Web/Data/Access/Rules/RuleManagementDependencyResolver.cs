using Microsoft.EntityFrameworkCore;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Data.Access.Rules;

internal static class RuleManagementDependencyResolver
{
    public static async Task<RuleManagementDependencies> ResolveAsync(
        ApplicationDbContext context,
        IReadOnlyList<Guid> tagIds,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tagIds);

        List<RuleTagEntry> tags = tagIds.Count == 0
            ? []
            : await context.Set<RuleTagEntry>()
                .Where(tag => tagIds.Contains(tag.PublicId))
                .OrderBy(static tag => tag.PublicId)
                .ToListAsync(cancellationToken);
        if (tags.Count != tagIds.Count)
        {
            HashSet<Guid> resolvedTagIds = [.. tags.Select(static tag => tag.PublicId)];
            Guid[] missingTagIds = [.. tagIds.Where(tagId => !resolvedTagIds.Contains(tagId))];
            return new RuleManagementDependencies(new RuleTagsNotFoundError(missingTagIds), [], Group: null);
        }

        RuleGroupEntry? group = null;
        if (groupId is { } requestedGroupId)
        {
            group = await context.Set<RuleGroupEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == requestedGroupId, cancellationToken);
            if (group is null)
            {
                return new RuleManagementDependencies(new RuleGroupNotFoundError(requestedGroupId), [], Group: null);
            }
        }

        return new RuleManagementDependencies(Error: null, tags, group);
    }
}
