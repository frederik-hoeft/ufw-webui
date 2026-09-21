using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Components.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Text;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Components.Rules;

public sealed partial class RuleListToolbar
{
    private static readonly DialogOptions s_filterDialogOptions = new()
    {
        CloseButton = true,
        FullWidth = true,
        MaxWidth = MaxWidth.Small,
    };

    private RuleQuery? _loadedQuery;
    private string _searchText = string.Empty;

    [Parameter, EditorRequired]
    public required RuleQuery Query { get; set; }

    [Parameter, EditorRequired]
    public FirewallAddressFamily AddressFamily { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<RuleQuery> QueryChanged { get; set; }

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_loadedQuery, Query))
        {
            return;
        }

        _loadedQuery = Query;
        _searchText = Query.Filters.OfType<TextRuleFilter>().FirstOrDefault()?.Text ?? string.Empty;
    }

    private Task SearchTextChangedAsync(string value)
    {
        _searchText = value;
        if (Disabled)
        {
            return Task.CompletedTask;
        }

        List<RuleFilter> filters = Query.Filters.ToList();
        int existingIndex = filters.FindIndex(static filter => filter is TextRuleFilter);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (existingIndex >= 0)
            {
                filters.RemoveAt(existingIndex);
            }
        }
        else if (existingIndex >= 0)
        {
            filters[existingIndex] = new TextRuleFilter(value);
        }
        else
        {
            filters.Insert(0, new TextRuleFilter(value));
        }

        return PublishQueryAsync(filters);
    }

    private async Task AddFilterAsync()
    {
        if (Disabled)
        {
            return;
        }

        RuleFilter? filter = await ShowFilterDialogAsync(RulesText["AddFilter"]);
        if (filter is null)
        {
            return;
        }

        List<RuleFilter> filters = [.. Query.Filters, filter];
        await PublishQueryAsync(filters);
    }

    private async Task EditFilterAsync(int index)
    {
        if (Disabled || index < 0 || index >= Query.Filters.Count)
        {
            return;
        }

        RuleFilter current = Query.Filters[index];
        RuleFilter? replacement = await ShowFilterDialogAsync(RulesText["EditFilter"], current);
        if (replacement is null)
        {
            return;
        }

        List<RuleFilter> filters = Query.Filters.ToList();
        filters[index] = replacement;
        await PublishQueryAsync(filters);
    }

    private Task RemoveFilterAsync(int index)
    {
        if (Disabled || index < 0 || index >= Query.Filters.Count)
        {
            return Task.CompletedTask;
        }

        List<RuleFilter> filters = Query.Filters.ToList();
        filters.RemoveAt(index);
        return PublishQueryAsync(filters);
    }

    private Task ClearAsync()
    {
        if (Disabled)
        {
            return Task.CompletedTask;
        }

        _loadedQuery = null;
        _searchText = string.Empty;
        return QueryChanged.InvokeAsync(RuleQuery.Empty);
    }

    private async Task<RuleFilter?> ShowFilterDialogAsync(string title, RuleFilter? filter = null)
    {
        DialogParameters<RuleFilterEditorDialog> parameters = [];
        parameters.Add(component => component.AddressFamily, AddressFamily);
        parameters.Add(component => component.Filter, filter);

        IDialogReference dialog = await DialogService.ShowAsync<RuleFilterEditorDialog>(title, parameters, s_filterDialogOptions);
        return await dialog.GetReturnValueAsync<RuleFilter?>();
    }

    private string DescribeFilter(RuleFilter filter)
    {
        RuleFilterDefinition definition = FilterCatalog.Resolve(filter);
        return definition.Describe(filter, RuleText, RulesText);
    }

    private Task PublishQueryAsync(IReadOnlyList<RuleFilter> filters)
    {
        _loadedQuery = null;
        return QueryChanged.InvokeAsync(filters.Count == 0 ? RuleQuery.Empty : new RuleQuery(filters));
    }
}
