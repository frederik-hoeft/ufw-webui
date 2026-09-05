using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

namespace Ufw.Client.Components;

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
