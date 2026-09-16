using System.Diagnostics.CodeAnalysis;
using Ufw.Client.Rules.Filtering;
using Ufw.Client.Rules.Filtering.Groups;

namespace Ufw.Client.Components.Rules.Filtering.Groups;

public sealed partial class GroupRuleFilterEditor : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private string _group = string.Empty;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedFilter, Filter))
        {
            return;
        }

        _loadedFilter = Filter;
        _group = Filter is GroupRuleFilter group ? group.Group : string.Empty;
    }

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        string group = _group.Trim();
        if (group.Length == 0)
        {
            filter = null;
            return false;
        }

        filter = new GroupRuleFilter(group);
        return true;
    }
}
