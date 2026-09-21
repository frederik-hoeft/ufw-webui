using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering.Directions;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Components.Rules.Filtering.Directions;

public sealed partial class DirectionRuleFilterEditor : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private FirewallDirection _direction = FirewallDirection.In;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedFilter, Filter))
        {
            return;
        }

        _loadedFilter = Filter;
        _direction = Filter is DirectionRuleFilter direction ? direction.Direction : FirewallDirection.In;
    }

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        filter = new DirectionRuleFilter(_direction);
        return true;
    }
}
