using Microsoft.JSInterop;
using Ufw.Web.Client.Services.Storage;

namespace Ufw.Web.Client.Tests.Services.Storage;

[TestClass]
public sealed class BrowserLocalStorageTests
{
    [TestMethod]
    public async Task BrowserLocalStorage_ForwardsGetSetAndRemoveToBrowserApiAsync()
    {
        StorageJsRuntime js = new() { GetResult = "stored" };
        BrowserLocalStorage storage = new(js);

        string? value = await storage.GetItemAsync("key", TestContext.CancellationToken);
        await storage.SetItemAsync("key", "value", TestContext.CancellationToken);
        await storage.RemoveItemAsync("key", TestContext.CancellationToken);

        Assert.AreEqual("stored", value);
        Assert.AreSequenceEqual(
            ["localStorage.getItem", "localStorage.setItem", "localStorage.removeItem"], js.Calls.Select(static call => call.Identifier).ToArray());
        Assert.AreSequenceEqual(["key"], js.Calls[0].Args);
        Assert.AreSequenceEqual(["key", "value"], js.Calls[1].Args);
        Assert.AreSequenceEqual(["key"], js.Calls[2].Args);
    }

    [TestMethod]
    public async Task BrowserLocalStorage_RejectsInvalidArgumentsBeforeJsInteropAsync()
    {
        StorageJsRuntime js = new();
        BrowserLocalStorage storage = new(js);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.GetItemAsync(" ", TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.SetItemAsync(" ", "value", TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => storage.SetItemAsync("key", null!, TestContext.CancellationToken).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.RemoveItemAsync("", TestContext.CancellationToken).AsTask());
        Assert.IsEmpty(js.Calls);
    }

    private sealed class StorageJsRuntime : IJSRuntime
    {
        public string? GetResult { get; init; }

        public List<(string Identifier, object?[] Args)> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add((identifier, args ?? []));
            object? result = identifier == "localStorage.getItem" ? GetResult : null;
            return ValueTask.FromResult((TValue)result!);
        }
    }

    public required TestContext TestContext { get; set; }
}
