using MudBlazor;

namespace Ufw.Client.Components.Layout;

public sealed class PageChromeState
{
    private object? _owner;
    private BreadcrumbItem[] _breadcrumbs = [];

    public IReadOnlyList<BreadcrumbItem> Breadcrumbs => _breadcrumbs;

    public event EventHandler? Changed;

    public void SetBreadcrumbs(object owner, IReadOnlyList<BreadcrumbItem> breadcrumbs)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(breadcrumbs);

        BreadcrumbItem[] snapshot = [.. breadcrumbs];
        _owner = owner;

        if (_breadcrumbs.SequenceEqual(snapshot))
        {
            return;
        }

        _breadcrumbs = snapshot;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ClearBreadcrumbs(object owner)
    {
        if (!ReferenceEquals(_owner, owner))
        {
            return;
        }

        _owner = null;
        if (_breadcrumbs.Length == 0)
        {
            return;
        }

        _breadcrumbs = [];
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
