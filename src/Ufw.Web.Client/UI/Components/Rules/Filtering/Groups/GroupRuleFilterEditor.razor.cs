using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Groups;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Groups;

public sealed partial class GroupRuleFilterEditor(IRuleGroupCatalogService catalog) : RuleFilterEditorBase
{
    private readonly CatalogRuleFilterEditorState<GroupRuleFilter, RuleGroup> _selection = new(
        static filter => filter.Group, static item => item.Id, static item => item.Name);

    protected async override Task OnInitializedAsync() => _selection.SetItems(await catalog.RefreshAsync(), Filter);

    protected override void OnParametersSet() => _selection.Synchronize(Filter);

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        filter = _selection.Selected is RuleGroup item ? new GroupRuleFilter(item) : null;
        return filter is not null;
    }

    private Task<IEnumerable<RuleGroup>> SearchAsync(string? value, CancellationToken cancellationToken) => _selection.SearchAsync(value, cancellationToken);

    private static string FormatGroup(RuleGroup? item) => item?.Name ?? string.Empty;
}
