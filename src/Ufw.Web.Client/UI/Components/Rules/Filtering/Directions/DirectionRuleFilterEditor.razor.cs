using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Directions;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Directions;

public sealed partial class DirectionRuleFilterEditor : RuleFilterEditorBase
{
    private readonly EnumRuleFilterEditorState<DirectionRuleFilter, FirewallDirection> _selection = new(
        FirewallDirection.In, static filter => filter.Direction, static value => new DirectionRuleFilter(value));

    protected override void OnParametersSet() => _selection.Synchronize(Filter);

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        filter = _selection.Build();
        return true;
    }
}
