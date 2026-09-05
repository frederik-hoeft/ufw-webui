using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ufw.Client.Theming;

internal sealed partial class BrowserClientThemeService(
    IJSRuntime jsRuntime,
    ILogger<BrowserClientThemeService> logger) : IClientThemeService
{
    private const string STORAGE_KEY = "ufw.theme";
    private const string LIGHT_VALUE = "light";
    private const string DARK_VALUE = "dark";

    private ClientThemeMode _mode = ClientThemeMode.Light;
    private bool _initialized;

    public ClientThemeMode Mode => _mode;

    public bool IsDarkMode => _mode is ClientThemeMode.Dark;

    public event Action? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        string? storedValue = null;
        try
        {
            storedValue = await jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                [STORAGE_KEY]);
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            LogThemePreferenceReadFailure(logger, exception);
        }

        _initialized = true;
        if (TryParse(storedValue, out ClientThemeMode storedMode) && storedMode != _mode)
        {
            _mode = storedMode;
            Changed?.Invoke();
        }
    }

    public async Task SetModeAsync(ClientThemeMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported client theme mode.");
        }

        if (_mode != mode)
        {
            _mode = mode;
            Changed?.Invoke();
        }

        try
        {
            await jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                cancellationToken,
                STORAGE_KEY,
                Serialize(mode));
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            LogThemePreferenceWriteFailure(logger, exception);
        }
    }

    public Task ToggleAsync(CancellationToken cancellationToken = default) =>
        SetModeAsync(IsDarkMode ? ClientThemeMode.Light : ClientThemeMode.Dark, cancellationToken);

    private static bool TryParse(string? value, out ClientThemeMode mode)
    {
        switch (value)
        {
            case LIGHT_VALUE:
                mode = ClientThemeMode.Light;
                return true;
            case DARK_VALUE:
                mode = ClientThemeMode.Dark;
                return true;
            default:
                mode = default;
                return false;
        }
    }

    private static string Serialize(ClientThemeMode mode) => mode switch
    {
        ClientThemeMode.Light => LIGHT_VALUE,
        ClientThemeMode.Dark => DARK_VALUE,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported client theme mode."),
    };

    [LoggerMessage(LogLevel.Debug, "Could not read the persisted browser theme preference; using the default light theme.")]
    private static partial void LogThemePreferenceReadFailure(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Could not persist the browser theme preference; keeping the selected theme for this session.")]
    private static partial void LogThemePreferenceWriteFailure(ILogger logger, Exception exception);
}
