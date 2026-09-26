using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Groups;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Groups;

public sealed partial class GroupRuleFilterEditor(IRuleGroupCatalogService groupCatalog) : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private IReadOnlyList<RuleGroup> _groups = [];
    private RuleGroup? _group;

    protected async override Task OnInitializedAsync()
    {
        _groups = await groupCatalog.RefreshAsync();
        SynchronizeFilter(force: true);
    }

    protected override void OnParametersSet() => SynchronizeFilter(force: false);

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        if (_group is null)
        {
            filter = null;
            return false;
        }

        filter = new GroupRuleFilter(_group);
        return true;
    }

    private Task<IEnumerable<RuleGroup>> SearchAsync(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<RuleGroup> matches = string.IsNullOrWhiteSpace(value)
            ? _groups
            : _groups.Where(group => group.Name.Contains(value.Trim(), StringComparison.CurrentCultureIgnoreCase));
        return Task.FromResult(matches);
    }

    private void SynchronizeFilter(bool force)
    {
        if (!force && ReferenceEquals(_loadedFilter, Filter))
        {
            return;
        }

        _loadedFilter = Filter;
        if (Filter is not GroupRuleFilter groupFilter)
        {
            _group = null;
            return;
        }

        _group = _groups.FirstOrDefault(group => group.Id == groupFilter.Group.Id) ?? groupFilter.Group;
    }

    private static string FormatGroup(RuleGroup? group) => group?.Name ?? string.Empty;
}
