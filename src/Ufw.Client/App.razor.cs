using Ufw.Client.Errors;

namespace Ufw.Client;

public sealed partial class App
{
    private StartupState _startupState = StartupState.Loading;
    private ClientError? _startupError;
    private bool _initializing;

    protected async override Task OnInitializedAsync()
    {
        Theme.Changed += OnThemeChanged;
        await Theme.InitializeAsync();
        await InitializeAsync();
    }

    public void Dispose() => Theme.Changed -= OnThemeChanged;

    private void OnThemeChanged() => StateHasChanged();

    private async Task InitializeAsync()
    {
        if (_initializing)
        {
            return;
        }

        _initializing = true;
        _startupState = StartupState.Loading;
        _startupError = null;
        try
        {
            await AuthenticationService.InitializeAsync();
            _startupState = StartupState.Ready;
        }
        catch (Exception exception)
        {
            _startupError = ClientErrors.Describe(exception);
            _startupState = StartupState.Failed;
        }
        finally
        {
            _initializing = false;
        }
    }

    private enum StartupState
    {
        Loading,
        Ready,
        Failed,
    }
}
