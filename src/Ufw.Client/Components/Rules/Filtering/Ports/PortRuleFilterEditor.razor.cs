using System.Diagnostics.CodeAnalysis;
using Ufw.Client.Rules.Filtering;
using Ufw.Client.Rules.Filtering.Ports;

namespace Ufw.Client.Components.Rules.Filtering.Ports;

public sealed partial class PortRuleFilterEditor : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private string _value = string.Empty;
    private string? _error;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedFilter, Filter))
        {
            return;
        }

        _loadedFilter = Filter;
        _value = Filter is PortRuleFilter ports ? ports.Ports.CanonicalValue : string.Empty;
        _error = null;
    }

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        _error = null;
        if (!RulePortSet.TryParse(_value, out RulePortSet? ports) || ports is null)
        {
            _error = RulesText["InvalidPortFilter"];
            filter = null;
            StateHasChanged();
            return false;
        }

        filter = new PortRuleFilter(RuleEndpointField.Any, ports);
        return true;
    }

    private void ValueChanged(string value)
    {
        _value = value;
        _error = null;
    }
}
