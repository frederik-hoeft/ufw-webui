namespace Ufw.Client.Theming;

internal interface IClientThemeService
{
    ClientThemeMode Mode { get; }

    bool IsDarkMode { get; }

    event Action? Changed;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SetModeAsync(ClientThemeMode mode, CancellationToken cancellationToken = default);

    Task ToggleAsync(CancellationToken cancellationToken = default);
}
