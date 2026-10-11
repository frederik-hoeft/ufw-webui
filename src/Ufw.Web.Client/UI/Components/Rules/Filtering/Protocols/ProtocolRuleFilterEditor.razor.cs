using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Protocols;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Protocols;

public sealed partial class ProtocolRuleFilterEditor : RuleFilterEditorBase
{
    private readonly EnumRuleFilterEditorState<ProtocolRuleFilter, FirewallProtocol> _selection = new(
        FirewallProtocol.Any, static filter => filter.Protocol, static value => new ProtocolRuleFilter(value));

    protected override void OnParametersSet() => _selection.Synchronize(Filter);

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        filter = _selection.Build();
        return true;
    }
}
