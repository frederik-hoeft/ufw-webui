using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Tags;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Tags;

public sealed partial class TagRuleFilterEditor(IRuleTagCatalogService catalog) : RuleFilterEditorBase
{
    private readonly CatalogRuleFilterEditorState<TagRuleFilter, RuleTag> _selection = new(
        static filter => filter.Tag, static item => item.Id, static item => item.Name);

    protected async override Task OnInitializedAsync() => _selection.SetItems(await catalog.RefreshAsync(), Filter);

    protected override void OnParametersSet() => _selection.Synchronize(Filter);

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        filter = _selection.Selected is RuleTag item ? new TagRuleFilter(item) : null;
        return filter is not null;
    }

    private Task<IEnumerable<RuleTag>> SearchAsync(string? value, CancellationToken cancellationToken) => _selection.SearchAsync(value, cancellationToken);

    private static string FormatTag(RuleTag? item) => item?.Name ?? string.Empty;
}
