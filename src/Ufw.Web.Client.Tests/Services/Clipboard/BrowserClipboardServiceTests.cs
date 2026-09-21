using Microsoft.JSInterop;
using Ufw.Web.Client.Services.Clipboard;

namespace Ufw.Web.Client.Tests.Services.Clipboard;

[TestClass]
public sealed class BrowserClipboardServiceTests
{
    [TestMethod]
    public async Task WriteTextAsync_UsesBrowserClipboardApi()
    {
        RecordingJsRuntime jsRuntime = new();
        BrowserClipboardService clipboard = new(jsRuntime);

        await clipboard.WriteTextAsync("ufw allow from any to any");

        Assert.HasCount(1, jsRuntime.Calls);
        Assert.AreEqual("navigator.clipboard.writeText", jsRuntime.Calls[0].Identifier);
        CollectionAssert.AreEqual(new object?[] { "ufw allow from any to any" }, jsRuntime.Calls[0].Args);
    }

    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public List<(string Identifier, object?[] Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((identifier, args ?? []));
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
