using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Components.Rules.Filtering.Actions;

public sealed partial class ActionRuleFilterEditor : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private FirewallAction _action = FirewallAction.Allow;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedFilter, Filter))
        {
            return;
        }

        _loadedFilter = Filter;
        _action = Filter is ActionRuleFilter action ? action.Action : FirewallAction.Allow;
    }

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        filter = new ActionRuleFilter(_action);
        return true;
    }
}
