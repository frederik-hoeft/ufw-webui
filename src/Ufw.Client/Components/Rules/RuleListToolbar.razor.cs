using Microsoft.AspNetCore.Components;
using Ufw.Client.Rules.Filtering;
using Ufw.Client.Rules.Filtering.Actions;
using Ufw.Client.Rules.Filtering.Directions;
using Ufw.Client.Rules.Filtering.Networks;
using Ufw.Client.Rules.Filtering.Ports;
using Ufw.Client.Rules.Filtering.Protocols;
using Ufw.Client.Rules.Filtering.Text;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

public sealed partial class RuleListToolbar
{
    private const string TCP_FILTER_VALUE = "tcp";
    private const string UDP_FILTER_VALUE = "udp";
    private const string ANY_FILTER_VALUE = "any";
    private const string ALLOW_FILTER_VALUE = "allow";
    private const string DENY_FILTER_VALUE = "deny";
    private const string REJECT_FILTER_VALUE = "reject";
    private const string LIMIT_FILTER_VALUE = "limit";
    private const string IN_FILTER_VALUE = "in";
    private const string OUT_FILTER_VALUE = "out";
    private const string FORWARD_FILTER_VALUE = "forward";
    private RuleQuery? _loadedQuery;
    private string _searchText = string.Empty;
    private string _sourceNetwork = string.Empty;
    private string _destinationNetwork = string.Empty;
    private string _ports = string.Empty;
    private string? _protocol;
    private string? _action;
    private string? _direction;
    private string? _sourceNetworkError;
    private string? _destinationNetworkError;
    private string? _portsError;

    [Parameter, EditorRequired]
    public required RuleQuery Query { get; set; }

    [Parameter, EditorRequired]
    public FirewallAddressFamily AddressFamily { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<RuleQuery> QueryChanged { get; set; }

    private IReadOnlyList<string> AppliedFilterLabels => Query.Filters.Select(DescribeFilter).ToArray();

    private bool HasFilterInput =>
        !string.IsNullOrWhiteSpace(_searchText)
        || !string.IsNullOrWhiteSpace(_sourceNetwork)
        || !string.IsNullOrWhiteSpace(_destinationNetwork)
        || !string.IsNullOrWhiteSpace(_ports)
        || _protocol is not null
        || _action is not null
        || _direction is not null;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedQuery, Query))
        {
            return;
        }

        _loadedQuery = Query;
        _searchText = Query.Filters.OfType<TextRuleFilter>().FirstOrDefault()?.Text ?? string.Empty;
        _sourceNetwork = Query.Filters.OfType<NetworkRuleFilter>().FirstOrDefault(static filter => filter.Endpoint == RuleEndpointField.Source)?.Network.CanonicalValue ?? string.Empty;
        _destinationNetwork = Query.Filters.OfType<NetworkRuleFilter>().FirstOrDefault(static filter => filter.Endpoint == RuleEndpointField.Destination)?.Network.CanonicalValue ?? string.Empty;
        _ports = Query.Filters.OfType<PortRuleFilter>().FirstOrDefault()?.Ports.CanonicalValue ?? string.Empty;
        _protocol = Query.Filters.OfType<ProtocolRuleFilter>().FirstOrDefault() is { } protocol ? RuleSpecificationNormalizer.FormatProtocol(protocol.Protocol) : null;
        _action = Query.Filters.OfType<ActionRuleFilter>().FirstOrDefault() is { } action ? RuleSpecificationNormalizer.FormatAction(action.Action) : null;
        _direction = Query.Filters.OfType<DirectionRuleFilter>().FirstOrDefault() is { } direction ? RuleSpecificationNormalizer.FormatDirection(direction.Direction) : null;
        ClearValidationErrors();
    }

    private Task SearchTextChangedAsync(string value)
    {
        _searchText = value;
        return ApplyCurrentQueryAsync();
    }

    private Task SourceNetworkChangedAsync(string value)
    {
        _sourceNetwork = value;
        return ApplyCurrentQueryAsync();
    }

    private Task DestinationNetworkChangedAsync(string value)
    {
        _destinationNetwork = value;
        return ApplyCurrentQueryAsync();
    }

    private Task PortsChangedAsync(string value)
    {
        _ports = value;
        return ApplyCurrentQueryAsync();
    }

    private Task ProtocolChangedAsync(string? value)
    {
        _protocol = value;
        return ApplyCurrentQueryAsync();
    }

    private Task ActionChangedAsync(string? value)
    {
        _action = value;
        return ApplyCurrentQueryAsync();
    }

    private Task DirectionChangedAsync(string? value)
    {
        _direction = value;
        return ApplyCurrentQueryAsync();
    }

    private async Task ApplyCurrentQueryAsync()
    {
        if (Disabled)
        {
            return;
        }

        ClearValidationErrors();
        List<RuleFilter> filters = [];
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            filters.Add(new TextRuleFilter(_searchText));
        }
        AddNetworkFilter(_sourceNetwork, RuleEndpointField.Source, filters, ref _sourceNetworkError);
        AddNetworkFilter(_destinationNetwork, RuleEndpointField.Destination, filters, ref _destinationNetworkError);
        if (!string.IsNullOrWhiteSpace(_ports))
        {
            if (!RulePortSet.TryParse(_ports, out RulePortSet? ports) || ports is null)
            {
                _portsError = RulesText["InvalidPortFilter"];
            }
            else
            {
                filters.Add(new PortRuleFilter(RuleEndpointField.Any, ports));
            }
        }

        AddEnumFilters(filters);
        if (_sourceNetworkError is not null || _destinationNetworkError is not null || _portsError is not null)
        {
            return;
        }

        await QueryChanged.InvokeAsync(new RuleQuery(filters));
    }

    private async Task ClearAsync()
    {
        if (Disabled)
        {
            return;
        }

        _loadedQuery = null;
        _searchText = string.Empty;
        _sourceNetwork = string.Empty;
        _destinationNetwork = string.Empty;
        _ports = string.Empty;
        _protocol = null;
        _action = null;
        _direction = null;
        ClearValidationErrors();
        await QueryChanged.InvokeAsync(RuleQuery.Empty);
    }

    private void AddNetworkFilter(string value, RuleEndpointField endpoint, List<RuleFilter> filters, ref string? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }
        if (!RuleNetwork.TryParse(value, out RuleNetwork? network) || network is null)
        {
            error = RulesText["InvalidNetworkFilter"];
            return;
        }
        if (network.AddressFamily != AddressFamily)
        {
            error = RulesText["WrongFamilyNetworkFilter", RuleText.FormatAddressFamily(AddressFamily)];
            return;
        }
        filters.Add(new NetworkRuleFilter(endpoint, network));
    }

    private void AddEnumFilters(List<RuleFilter> filters)
    {
        if (_protocol is not null)
        {
            filters.Add(new ProtocolRuleFilter(_protocol switch
            {
                TCP_FILTER_VALUE => FirewallProtocol.Tcp,
                UDP_FILTER_VALUE => FirewallProtocol.Udp,
                _ => FirewallProtocol.Any,
            }));
        }
        if (_action is not null)
        {
            filters.Add(new ActionRuleFilter(_action switch
            {
                DENY_FILTER_VALUE => FirewallAction.Deny,
                REJECT_FILTER_VALUE => FirewallAction.Reject,
                LIMIT_FILTER_VALUE => FirewallAction.Limit,
                _ => FirewallAction.Allow,
            }));
        }
        if (_direction is not null)
        {
            filters.Add(new DirectionRuleFilter(_direction switch
            {
                OUT_FILTER_VALUE => FirewallDirection.Out,
                FORWARD_FILTER_VALUE => FirewallDirection.Forward,
                _ => FirewallDirection.In,
            }));
        }
    }

    private string DescribeFilter(RuleFilter filter) => filter switch
    {
        TextRuleFilter text => $"{RulesText["SearchRules"]}: {text.Text}",
        NetworkRuleFilter network when network.Endpoint == RuleEndpointField.Source => $"{RulesText["FromColumn"]}: {network.Network.CanonicalValue}",
        NetworkRuleFilter network => $"{RulesText["ToColumn"]}: {network.Network.CanonicalValue}",
        PortRuleFilter ports => $"{RulesText["PortFilter"]}: {ports.Ports.CanonicalValue}",
        ProtocolRuleFilter protocol => $"{RulesText["ProtocolColumn"]}: {RuleText.FormatProtocol(protocol.Protocol)}",
        ActionRuleFilter action => $"{RulesText["ActionColumn"]}: {RuleText.FormatAction(action.Action)}",
        DirectionRuleFilter direction => $"{RulesText["DirectionColumn"]}: {RuleText.FormatDirection(direction.Direction)}",
        _ => filter.GetType().Name,
    };

    private void ClearValidationErrors()
    {
        _sourceNetworkError = null;
        _destinationNetworkError = null;
        _portsError = null;
    }
}
