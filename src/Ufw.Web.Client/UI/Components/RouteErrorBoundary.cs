using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components;

namespace Ufw.Web.Client.UI.Components;

public sealed class RouteErrorBoundary : ErrorBoundary, IDisposable
{
    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += HandleLocationChanged;
        base.OnInitialized();
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= HandleLocationChanged;
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _ = InvokeAsync(Recover);
    }
}
