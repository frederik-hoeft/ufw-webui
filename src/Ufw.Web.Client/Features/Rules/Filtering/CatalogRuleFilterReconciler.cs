namespace Ufw.Web.Client.Features.Rules.Filtering;

internal static class CatalogRuleFilterReconciler
{
    public static RuleQuery Reconcile<TFilter, TItem>(
        RuleQuery query,
        IReadOnlyList<TItem> currentItems,
        Func<TFilter, TItem> getItem,
        Func<TItem, Guid> getId,
        Func<TItem, TFilter> createFilter)
        where TFilter : RuleFilter
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(currentItems);

        Dictionary<Guid, TItem> itemsById = currentItems.ToDictionary(getId);
        List<RuleFilter> filters = new(query.Filters.Count);
        foreach (RuleFilter filter in query.Filters)
        {
            if (filter is not TFilter typedFilter)
            {
                filters.Add(filter);
                continue;
            }

            if (itemsById.TryGetValue(getId(getItem(typedFilter)), out TItem? currentItem))
            {
                filters.Add(createFilter(currentItem));
            }
        }

        return filters.Count == 0 ? RuleQuery.Empty : new RuleQuery(filters);
    }
}
