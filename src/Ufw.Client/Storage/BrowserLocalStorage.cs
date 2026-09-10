using Microsoft.JSInterop;

namespace Ufw.Client.Storage;

internal sealed class BrowserLocalStorage(IJSRuntime jsRuntime) : ILocalStorage
{
    public ValueTask<string?> GetItemAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, [key]);
    }

    public ValueTask SetItemAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        return jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, value);
    }

    public ValueTask RemoveItemAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key);
    }
}
