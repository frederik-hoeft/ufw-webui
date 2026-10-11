using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Actions;

public sealed partial class ActionRuleFilterEditor : RuleFilterEditorBase
{
    private readonly EnumRuleFilterEditorState<ActionRuleFilter, FirewallAction> _selection = new(
        FirewallAction.Allow, static filter => filter.Action, static value => new ActionRuleFilter(value));

    protected override void OnParametersSet() => _selection.Synchronize(Filter);

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        filter = _selection.Build();
        return true;
    }
}
