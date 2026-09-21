using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Client.Features.Rules.Filtering.Tags;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Filtering.Tags;

public sealed partial class TagRuleFilterEditor(IRuleTagCatalogService tagCatalog) : RuleFilterEditorBase
{
    private RuleFilter? _loadedFilter;
    private IReadOnlyList<RuleTag> _tags = [];
    private RuleTag? _tag;

    protected async override Task OnInitializedAsync()
    {
        _tags = await tagCatalog.RefreshAsync();
        SynchronizeFilter(force: true);
    }

    protected override void OnParametersSet() => SynchronizeFilter(force: false);

    public override bool TryBuildFilter([NotNullWhen(true)] out RuleFilter? filter)
    {
        if (_tag is null)
        {
            filter = null;
            return false;
        }

        filter = new TagRuleFilter(_tag);
        return true;
    }

    private Task<IEnumerable<RuleTag>> SearchAsync(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<RuleTag> matches = string.IsNullOrWhiteSpace(value)
            ? _tags
            : _tags.Where(tag => tag.Name.Contains(value.Trim(), StringComparison.CurrentCultureIgnoreCase));
        return Task.FromResult(matches);
    }

    private void SynchronizeFilter(bool force)
    {
        if (!force && ReferenceEquals(_loadedFilter, Filter))
        {
            return;
        }

        _loadedFilter = Filter;
        if (Filter is not TagRuleFilter tagFilter)
        {
            _tag = null;
            return;
        }

        _tag = _tags.FirstOrDefault(tag => tag.Id == tagFilter.Tag.Id) ?? tagFilter.Tag;
    }

    private static string FormatTag(RuleTag? tag) => tag?.Name ?? string.Empty;
}
