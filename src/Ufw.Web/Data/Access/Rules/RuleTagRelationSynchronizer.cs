using Ufw.Web.Data.Model;

namespace Ufw.Web.Data.Access.Rules;

internal static class RuleTagRelationSynchronizer
{
    public static void Synchronize<TRelation>(
        ApplicationDbContext context,
        ICollection<TRelation> relations,
        IReadOnlyList<RuleTagEntry> desiredTags,
        Func<TRelation, long> tagIdSelector,
        Func<RuleTagEntry, TRelation> relationFactory)
        where TRelation : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(relations);
        ArgumentNullException.ThrowIfNull(desiredTags);
        ArgumentNullException.ThrowIfNull(tagIdSelector);
        ArgumentNullException.ThrowIfNull(relationFactory);

        HashSet<long> desiredTagIds = [.. desiredTags.Select(static tag => tag.Id)];
        TRelation[] removed = [.. relations.Where(relation => !desiredTagIds.Contains(tagIdSelector(relation)))];
        foreach (TRelation relation in removed)
        {
            relations.Remove(relation);
        }
        context.RemoveRange(removed);

        HashSet<long> existingTagIds = [.. relations.Select(tagIdSelector)];
        foreach (RuleTagEntry tag in desiredTags.Where(tag => !existingTagIds.Contains(tag.Id)))
        {
            relations.Add(relationFactory(tag));
        }
    }
}
