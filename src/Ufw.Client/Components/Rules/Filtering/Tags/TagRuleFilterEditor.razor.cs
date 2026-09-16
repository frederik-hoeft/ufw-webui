using System.Diagnostics.CodeAnalysis;
using Ufw.Client.Rules.Filtering;
using Ufw.Client.Rules.Filtering.Tags;

namespace Ufw.Client.Components.Rules.Filtering.Tags;

public sealed partial class TagRuleFilterEditor : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private string _tag = string.Empty;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedFilter, Filter))
        {
            return;
        }

        _loadedFilter = Filter;
        _tag = Filter is TagRuleFilter tag ? tag.Tag : string.Empty;
    }

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        string tag = _tag.Trim();
        if (tag.Length == 0)
        {
            filter = null;
            return false;
        }

        filter = new TagRuleFilter(tag);
        return true;
    }
}
