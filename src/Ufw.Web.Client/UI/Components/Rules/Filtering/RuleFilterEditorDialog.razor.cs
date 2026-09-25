using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering;

public sealed partial class RuleFilterEditorDialog
{
    private RuleFilterDefinition? _selectedDefinition;
    private DynamicComponent? _dynamicEditor;
    private string _selectorSearch = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public FirewallAddressFamily AddressFamily { get; set; }

    [Parameter]
    public RuleFilter? Filter { get; set; }

    private IDictionary<string, object> EditorParameters
    {
        get
        {
            if (_selectedDefinition is null)
            {
                return new Dictionary<string, object>();
            }

            Dictionary<string, object> parameters = new(_selectedDefinition.Parameters)
            {
                [nameof(RuleFilterEditorBase.AddressFamily)] = AddressFamily,
            };
            if (Filter is not null)
            {
                parameters[nameof(RuleFilterEditorBase.Filter)] = Filter;
            }
            return parameters;
        }
    }

    private IReadOnlyList<RuleFilterDefinitionGroup> VisibleGroups
    {
        get
        {
            IEnumerable<RuleFilterDefinition> definitions = FilterCatalog.Definitions.Where(static definition => definition.Selectable);
            if (!string.IsNullOrWhiteSpace(_selectorSearch))
            {
                string search = _selectorSearch.Trim();
                definitions = definitions.Where(definition => MatchesSearch(definition, search));
            }

            return definitions
                .GroupBy(definition => RulesText[definition.CategoryResourceKey].Value, StringComparer.CurrentCultureIgnoreCase)
                .Select(static group => new RuleFilterDefinitionGroup(group.Key, group.ToArray()))
                .ToArray();
        }
    }

    protected override void OnParametersSet()
    {
        if (Filter is not null)
        {
            _selectedDefinition = FilterCatalog.Resolve(Filter);
        }
    }

    private bool MatchesSearch(RuleFilterDefinition definition, string search)
    {
        return RulesText[definition.DisplayNameResourceKey].Value.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || RulesText[definition.CategoryResourceKey].Value.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || definition.Key.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectorSearchChanged(string value) => _selectorSearch = value;

    private void SelectDefinition(RuleFilterDefinition definition)
    {
        _selectedDefinition = definition;
        _dynamicEditor = null;
    }

    private void BackToSelector()
    {
        _selectedDefinition = null;
        _dynamicEditor = null;
    }

    private void Cancel() => MudDialog.Cancel();

    private void Apply()
    {
        if (_dynamicEditor?.Instance is IRuleFilterEditor editor && editor.TryBuildFilter(out RuleFilter? filter))
        {
            MudDialog.Close(DialogResult.Ok(filter));
        }
    }

    private sealed record RuleFilterDefinitionGroup(string Category, IReadOnlyList<RuleFilterDefinition> Definitions);
}
