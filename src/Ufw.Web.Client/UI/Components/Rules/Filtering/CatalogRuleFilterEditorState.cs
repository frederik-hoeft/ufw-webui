using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering;

internal sealed class CatalogRuleFilterEditorState<TFilter, TItem>(
    Func<TFilter, TItem> getItem,
    Func<TItem, Guid> getId,
    Func<TItem, string> getName)
    where TFilter : RuleFilter
    where TItem : class
{
    private RuleFilter? _loadedFilter;
    private IReadOnlyList<TItem> _items = [];

    public TItem? Selected { get; set; }

    public void SetItems(IReadOnlyList<TItem> items, RuleFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
        Synchronize(filter, force: true);
    }

    public void Synchronize(RuleFilter? filter, bool force = false)
    {
        if (!force && ReferenceEquals(_loadedFilter, filter))
        {
            return;
        }

        _loadedFilter = filter;
        if (filter is not TFilter typedFilter)
        {
            Selected = null;
            return;
        }

        TItem item = getItem(typedFilter);
        Selected = _items.FirstOrDefault(candidate => getId(candidate) == getId(item)) ?? item;
    }

    public Task<IEnumerable<TItem>> SearchAsync(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<TItem> matches = string.IsNullOrWhiteSpace(value)
            ? _items
            : _items.Where(item => getName(item).Contains(value.Trim(), StringComparison.CurrentCultureIgnoreCase));
        return Task.FromResult(matches);
    }
}
