using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering.Protocols;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Components.Rules.Filtering.Protocols;

public sealed partial class ProtocolRuleFilterEditor : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private FirewallProtocol _protocol = FirewallProtocol.Any;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedFilter, Filter))
        {
            return;
        }

        _loadedFilter = Filter;
        _protocol = Filter is ProtocolRuleFilter protocol ? protocol.Protocol : FirewallProtocol.Any;
    }

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        filter = new ProtocolRuleFilter(_protocol);
        return true;
    }
}
