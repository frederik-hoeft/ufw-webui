using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Rules.Filtering.Tags;

internal sealed class RuleTagFilterReconciler : IRuleTagFilterReconciler
{
    public RuleQuery Reconcile(RuleQuery query, IReadOnlyList<RuleTag> currentTags)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(currentTags);

        Dictionary<Guid, RuleTag> tagsById = currentTags.ToDictionary(static tag => tag.Id);
        List<RuleFilter> filters = new(query.Filters.Count);
        foreach (RuleFilter filter in query.Filters)
        {
            if (filter is not TagRuleFilter tagFilter)
            {
                filters.Add(filter);
                continue;
            }

            if (tagsById.TryGetValue(tagFilter.Tag.Id, out RuleTag? currentTag))
            {
                filters.Add(new TagRuleFilter(currentTag));
            }
        }

        return filters.Count == 0 ? RuleQuery.Empty : new RuleQuery(filters);
    }
}
