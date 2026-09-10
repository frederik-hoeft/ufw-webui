using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ufw.Client.Components;

public sealed partial class ThemeToggle
{
    private bool _busy;

    [Parameter]
    public Color Color { get; set; } = Color.Default;

    [Parameter]
    public Variant Variant { get; set; } = Variant.Text;

    [Parameter]
    public bool ShowLabel { get; set; }

    [Parameter]
    public string? Class { get; set; }

    private string Icon => Theme.IsDarkMode
        ? Icons.Material.Filled.DarkMode
        : Icons.Material.Filled.LightMode;

    private string Label => Theme.IsDarkMode
        ? CommonText["ThemeEnabledSwitchLight"]
        : CommonText["ThemeEnabledSwitchDark"];

    private async Task ToggleAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            await Theme.ToggleAsync();
        }
        finally
        {
            _busy = false;
        }
    }
}
