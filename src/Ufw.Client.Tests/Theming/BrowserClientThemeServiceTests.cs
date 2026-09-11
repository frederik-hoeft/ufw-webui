using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using Ufw.Client.Storage;
using Ufw.Client.Theming;

namespace Ufw.Client.Tests.Theming;

[TestClass]
public sealed class BrowserClientThemeServiceTests
{
    [TestMethod]
    public async Task InitializeAsync_LoadsStoredDarkPreferenceOnceAndNotifiesAsync()
    {
        Mock<ILocalStorage> storage = new();
        storage.Setup(service => service.GetItemAsync("ufw.theme", It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<string?>("dark"));
        BrowserClientThemeService service = new(storage.Object, NullLogger<BrowserClientThemeService>.Instance);
        int changed = 0;
        service.Changed += () => changed++;

        await service.InitializeAsync();
        await service.InitializeAsync();

        Assert.AreEqual(ClientThemeMode.Dark, service.Mode);
        Assert.IsTrue(service.IsDarkMode);
        Assert.AreEqual(1, changed);
        storage.Verify(service => service.GetItemAsync("ufw.theme", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InitializeAsync_StorageFailureFallsBackToLightWithoutFailingAsync()
    {
        Mock<ILocalStorage> storage = new();
        storage.Setup(service => service.GetItemAsync("ufw.theme", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException<string?>(new JSException("blocked")));
        BrowserClientThemeService service = new(storage.Object, NullLogger<BrowserClientThemeService>.Instance);

        await service.InitializeAsync();

        Assert.AreEqual(ClientThemeMode.Light, service.Mode);
    }

    [TestMethod]
    public async Task SetModeAsync_UpdatesSessionEvenWhenPersistenceFailsAsync()
    {
        Mock<ILocalStorage> storage = new();
        storage.Setup(service => service.SetItemAsync("ufw.theme", "dark", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException(new InvalidOperationException("storage unavailable")));
        BrowserClientThemeService service = new(storage.Object, NullLogger<BrowserClientThemeService>.Instance);
        int changed = 0;
        service.Changed += () => changed++;

        await service.SetModeAsync(ClientThemeMode.Dark);

        Assert.AreEqual(ClientThemeMode.Dark, service.Mode);
        Assert.AreEqual(1, changed);
    }

    [TestMethod]
    public async Task ToggleAsync_AlternatesModesAndPersistsEachSelectionAsync()
    {
        Mock<ILocalStorage> storage = new();
        storage.Setup(service => service.SetItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.CompletedTask);
        BrowserClientThemeService service = new(storage.Object, NullLogger<BrowserClientThemeService>.Instance);

        await service.ToggleAsync();
        Assert.AreEqual(ClientThemeMode.Dark, service.Mode);
        await service.ToggleAsync();
        Assert.AreEqual(ClientThemeMode.Light, service.Mode);

        storage.Verify(service => service.SetItemAsync("ufw.theme", "dark", It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(service => service.SetItemAsync("ufw.theme", "light", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SetModeAsync_InvalidEnumIsRejectedWithoutPersistenceAsync()
    {
        Mock<ILocalStorage> storage = new();
        BrowserClientThemeService service = new(storage.Object, NullLogger<BrowserClientThemeService>.Instance);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => service.SetModeAsync((ClientThemeMode)42));

        storage.VerifyNoOtherCalls();
    }
}
