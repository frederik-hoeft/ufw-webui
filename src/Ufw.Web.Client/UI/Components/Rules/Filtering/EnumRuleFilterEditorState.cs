using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering;

internal sealed class EnumRuleFilterEditorState<TFilter, TValue>(
    TValue defaultValue,
    Func<TFilter, TValue> getValue,
    Func<TValue, TFilter> createFilter)
    where TFilter : RuleFilter
    where TValue : struct, Enum
{
    private readonly TValue _defaultValue = defaultValue;
    private RuleFilter? _loadedFilter;

    public TValue Value { get; set; } = defaultValue;

    public void Synchronize(RuleFilter? filter)
    {
        if (ReferenceEquals(_loadedFilter, filter))
        {
            return;
        }

        _loadedFilter = filter;
        Value = filter is TFilter typedFilter ? getValue(typedFilter) : _defaultValue;
    }

    public TFilter Build() => createFilter(Value);
}
