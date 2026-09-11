using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ufw.Client.Components.Layout;

public sealed partial class PageHeader
{
    private PageChromeState? _registeredPageChrome;

    [CascadingParameter]
    public PageChromeState? PageChrome { get; set; }

    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string? Description { get; set; }

    [Parameter]
    public IReadOnlyList<BreadcrumbItem> Breadcrumbs { get; set; } = [];

    [Parameter]
    public RenderFragment? Actions { get; set; }

    [Parameter]
    public RenderFragment? Status { get; set; }

    [Parameter]
    public bool ShowStatus { get; set; } = true;

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_registeredPageChrome, PageChrome))
        {
            _registeredPageChrome?.ClearBreadcrumbs(this);
            _registeredPageChrome = PageChrome;
        }

        _registeredPageChrome?.SetBreadcrumbs(this, Breadcrumbs);
    }

    public void Dispose() => _registeredPageChrome?.ClearBreadcrumbs(this);
}
