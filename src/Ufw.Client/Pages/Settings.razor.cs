using MudBlazor;
using Ufw.Client.Theming;

namespace Ufw.Client.Pages;

public sealed partial class Settings
{
    private bool _themeBusy;

    private IReadOnlyList<BreadcrumbItem> Breadcrumbs =>
    [
        new BreadcrumbItem(SettingsText["SystemBreadcrumb"], null, disabled: true),
        new BreadcrumbItem(SettingsText["Title"], null, disabled: true),
    ];

    protected override void OnInitialized() => Theme.Changed += OnThemeChanged;

    public void Dispose() => Theme.Changed -= OnThemeChanged;

    private void OnThemeChanged() => _ = InvokeAsync(StateHasChanged);

    private async Task SetThemeAsync(ClientThemeMode mode)
    {
        if (_themeBusy || Theme.Mode == mode)
        {
            return;
        }

        _themeBusy = true;
        try
        {
            await Theme.SetModeAsync(mode);
        }
        finally
        {
            _themeBusy = false;
        }
    }

    private Task SetCultureAsync(string cultureName) => Culture.SetCultureAsync(cultureName);
}
