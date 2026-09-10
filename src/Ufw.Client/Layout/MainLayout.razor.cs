using MudBlazor;
using Ufw.Client.Components.Layout;
using Ufw.Client.Errors;

namespace Ufw.Client.Layout;

public sealed partial class MainLayout
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly PageChromeState _pageChrome = new();
    private bool _drawerOpen = true;
    private bool _logoutBusy;

    protected override void OnInitialized() => _pageChrome.Changed += OnPageChromeChanged;

    public void Dispose()
    {
        _pageChrome.Changed -= OnPageChromeChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    private void OnPageChromeChanged(object? sender, EventArgs args) => _ = InvokeAsync(StateHasChanged);

    private async Task LogoutAsync()
    {
        if (_logoutBusy)
        {
            return;
        }

        _logoutBusy = true;
        try
        {
            await AuthenticationService.LogoutAsync(_lifetime.Token);
            Navigation.NavigateTo("login", replace: true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            ClientError error = ClientErrors.Describe(exception);
            Snackbar.Add(CommonText["CouldNotSignOut", error.Message], Severity.Error);
        }
        finally
        {
            _logoutBusy = false;
        }
    }
}
