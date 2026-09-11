using Microsoft.JSInterop;
using Ufw.Client.Storage;

namespace Ufw.Client.Tests;

[TestClass]
public sealed class BrowserLocalStorageTests
{
    [TestMethod]
    public async Task BrowserLocalStorage_ForwardsGetSetAndRemoveToBrowserApiAsync()
    {
        StorageJsRuntime js = new() { GetResult = "stored" };
        BrowserLocalStorage storage = new(js);

        string? value = await storage.GetItemAsync("key");
        await storage.SetItemAsync("key", "value");
        await storage.RemoveItemAsync("key");

        Assert.AreEqual("stored", value);
        CollectionAssert.AreEqual(
            new[] { "localStorage.getItem", "localStorage.setItem", "localStorage.removeItem" },
            js.Calls.Select(static call => call.Identifier).ToArray());
        CollectionAssert.AreEqual(new object?[] { "key" }, js.Calls[0].Args);
        CollectionAssert.AreEqual(new object?[] { "key", "value" }, js.Calls[1].Args);
        CollectionAssert.AreEqual(new object?[] { "key" }, js.Calls[2].Args);
    }

    [TestMethod]
    public async Task BrowserLocalStorage_RejectsInvalidArgumentsBeforeJsInteropAsync()
    {
        StorageJsRuntime js = new();
        BrowserLocalStorage storage = new(js);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.GetItemAsync(" ").AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.SetItemAsync(" ", "value").AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => storage.SetItemAsync("key", null!).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => storage.RemoveItemAsync("").AsTask());
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
}
