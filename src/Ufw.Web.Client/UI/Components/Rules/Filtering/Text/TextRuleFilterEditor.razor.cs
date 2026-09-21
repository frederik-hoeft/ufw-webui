using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Client.Features.Rules.Filtering.Text;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Text;

public sealed partial class TextRuleFilterEditor : RuleFilterEditorBase
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
        _value = Filter is TextRuleFilter text ? text.Text : string.Empty;
        _error = null;
    }

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        _error = null;
        if (string.IsNullOrWhiteSpace(_value))
        {
            _error = RulesText["FilterValueRequired"];
            filter = null;
            StateHasChanged();
            return false;
        }

        filter = new TextRuleFilter(_value);
        return true;
    }

    private void ValueChanged(string value)
    {
        _value = value;
        _error = null;
    }
}
