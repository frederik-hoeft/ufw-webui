using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Moq;
using System.Globalization;
using Ufw.Client.Localization;
using Ufw.Client.Storage;
using Ufw.Client.Tests.Support;

namespace Ufw.Client.Tests.Localization;

[TestClass]
[DoNotParallelize]
public sealed class BrowserClientCultureServiceTests
{
    [TestMethod]
    public async Task SetCultureAsync_PersistsSupportedChangeThenForcesReloadAsync()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Mock<ILocalStorage> storage = CreateStorage();
            TestNavigationManager navigation = new();
            BrowserClientCultureService service = CreateService(storage, navigation);

            await service.SetCultureAsync("de-DE");

            storage.Verify(localStorage => localStorage.SetItemAsync("ufw.culture", "de-DE", It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual("https://localhost/app", navigation.LastUri);
            Assert.IsTrue(navigation.LastForceLoad);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [TestMethod]
    public async Task SetCultureAsync_CurrentCultureDoesNothingAsync()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Mock<ILocalStorage> storage = CreateStorage();
            TestNavigationManager navigation = new();
            BrowserClientCultureService service = CreateService(storage, navigation);

            await service.SetCultureAsync("EN-us");

            storage.VerifyNoOtherCalls();
            Assert.IsNull(navigation.LastUri);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [TestMethod]
    public async Task SetCultureAsync_UnsupportedCultureIsRejectedBeforeStorageAsync()
    {
        Mock<ILocalStorage> storage = CreateStorage();
        BrowserClientCultureService service = CreateService(storage, new TestNavigationManager());

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => service.SetCultureAsync("fr-FR"));

        storage.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task SetCultureAsync_StorageFailurePropagatesAndDoesNotReloadAsync()
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Mock<ILocalStorage> storage = new();
            storage.Setup(localStorage => localStorage.SetItemAsync("ufw.culture", "de-DE", It.IsAny<CancellationToken>()))
                .Returns(() => ValueTask.FromException(new JSException("blocked")));
            TestNavigationManager navigation = new();
            BrowserClientCultureService service = CreateService(storage, navigation);

            await Assert.ThrowsExactlyAsync<JSException>(() => service.SetCultureAsync("de-DE"));

            Assert.IsNull(navigation.LastUri);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static Mock<ILocalStorage> CreateStorage()
    {
        Mock<ILocalStorage> storage = new();
        storage.Setup(localStorage => localStorage.SetItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.CompletedTask);
        return storage;
    }

    private static BrowserClientCultureService CreateService(Mock<ILocalStorage> storage, TestNavigationManager navigation) =>
        new(ClientLocalizationOptionsTests.CreateOptions("en-US", "en-US", "de-DE"), storage.Object, navigation, NullLogger<BrowserClientCultureService>.Instance);
}
