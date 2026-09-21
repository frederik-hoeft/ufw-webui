using Microsoft.JSInterop;

namespace Ufw.Web.Client.Services.Clipboard;

internal sealed class BrowserClipboardService(IJSRuntime jsRuntime) : IClipboardService
{
    public ValueTask WriteTextAsync(string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", cancellationToken, value);
    }
}
