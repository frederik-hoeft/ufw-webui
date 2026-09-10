using Microsoft.AspNetCore.Components;

namespace Ufw.Client.Components.Rules;

public sealed partial class MutationAuthorizationField
{
    [Parameter]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    private async Task ValueChangedAsync(string? value)
    {
        Value = value ?? string.Empty;
        await ValueChanged.InvokeAsync(Value);
    }
}
