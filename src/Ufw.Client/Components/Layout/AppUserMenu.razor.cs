using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ufw.Client.Components.Layout;

public sealed partial class AppUserMenu
{
    private bool _themeBusy;

    [Parameter, EditorRequired]
    public string UserName { get; set; } = string.Empty;

    [Parameter]
    public bool SignOutBusy { get; set; }

    [Parameter, EditorRequired]
    public EventCallback SignOut { get; set; }

    private string ThemeActionIcon => Theme.IsDarkMode
        ? Icons.Material.Filled.LightMode
        : Icons.Material.Filled.DarkMode;

    private string ThemeActionLabel => Theme.IsDarkMode
        ? CommonText["ToggleThemeToLight"]
        : CommonText["ToggleThemeToDark"];

    private async Task ToggleThemeAsync()
    {
        if (_themeBusy || SignOutBusy)
        {
            return;
        }

        _themeBusy = true;
        try
        {
            await Theme.ToggleAsync();
        }
        finally
        {
            _themeBusy = false;
        }
    }

    private async Task SignOutAsync()
    {
        if (_themeBusy || SignOutBusy)
        {
            return;
        }

        await SignOut.InvokeAsync();
    }
}
