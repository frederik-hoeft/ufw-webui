using Microsoft.AspNetCore.Components;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts.Model;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Features.Rules.Filtering.Networks;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Networks;

public sealed partial class NetworkRuleFilterEditor(IKnownHostInventoryService knownHosts) : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private bool? _loadedSourceEndpoint;
    private string _value = string.Empty;
    private string? _error;
    private IReadOnlyList<KnownHostInventoryItem> _suggestions = [];

    [Parameter]
    public bool SourceEndpoint { get; set; }

    private RuleEndpointField Endpoint => SourceEndpoint ? RuleEndpointField.Source : RuleEndpointField.Destination;

    private string EditorLabel => SourceEndpoint
        ? RulesText["SourceNetworkFilter"]
        : RulesText["DestinationNetworkFilter"];

    private string Placeholder => AddressFamily == FirewallAddressFamily.IPv6 ? "2001:db8::/32" : "10.0.0.0/8";

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedFilter, Filter) && _loadedSourceEndpoint == SourceEndpoint)
        {
            return;
        }

        _loadedFilter = Filter;
        _loadedSourceEndpoint = SourceEndpoint;
        _suggestions = knownHosts.Current?.Hosts.Where(static host => host.IsVisible).ToArray() ?? [];
        _value = Filter is NetworkRuleFilter network && network.Endpoint == Endpoint ? network.Network.CanonicalValue : string.Empty;
        _error = null;
    }

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        _error = null;
        if (!RuleNetwork.TryParse(_value, out RuleNetwork? network) || network is null)
        {
            _error = RulesText["InvalidNetworkFilter"];
            filter = null;
            StateHasChanged();
            return false;
        }
        if (network.AddressFamily != AddressFamily)
        {
            _error = RulesText["WrongFamilyNetworkFilter", RuleText.FormatAddressFamily(AddressFamily)];
            filter = null;
            StateHasChanged();
            return false;
        }

        filter = new NetworkRuleFilter(Endpoint, network);
        return true;
    }

    private void ValueChanged(string? value)
    {
        _value = value ?? string.Empty;
        _error = null;
    }
}
