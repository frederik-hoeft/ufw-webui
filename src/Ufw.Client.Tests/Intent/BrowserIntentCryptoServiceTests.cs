using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Ufw.Client.Errors;
using Ufw.Client.Intent;
using Ufw.Client.Tests.Support;

namespace Ufw.Client.Tests.Intent;

[TestClass]
public sealed class BrowserIntentCryptoServiceTests
{
    [TestMethod]
    public async Task Operations_ImportModuleOnceAndForwardExactArgumentsAsync()
    {
        RecordingJsRuntime js = new();
        js.Module.SetResult("getKeyId", "key-id");
        js.Module.SetResult("createNonce", "nonce");
        js.Module.SetResult("sign", "signature");
        await using BrowserIntentCryptoService service = new(js, NullLogger<BrowserIntentCryptoService>.Instance);
        byte[] payload = [1, 2, 3];

        Assert.AreEqual("key-id", await service.GetKeyIdAsync("private"));
        Assert.AreEqual("nonce", await service.CreateNonceAsync(16));
        Assert.AreEqual("signature", await service.SignAsync("private", payload));

        Assert.AreEqual(1, js.ImportCount);
        Assert.AreEqual("private", js.Module.Calls.Single(call => call.Identifier == "getKeyId").Args[0]);
        Assert.AreEqual(16, js.Module.Calls.Single(call => call.Identifier == "createNonce").Args[0]);
        CollectionAssert.AreEqual(payload, (byte[])js.Module.Calls.Single(call => call.Identifier == "sign").Args[1]!);
    }

    [TestMethod]
    public async Task ImportFailure_IsTranslatedToBrowserOperationExceptionAsync()
    {
        RecordingJsRuntime js = new() { ImportException = new JSException("blocked") };
        await using BrowserIntentCryptoService service = new(js, NullLogger<BrowserIntentCryptoService>.Instance);

        BrowserOperationException exception = await Assert.ThrowsExactlyAsync<BrowserOperationException>(() => service.GetKeyIdAsync("private"));

        Assert.IsInstanceOfType<JSException>(exception.InnerException);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesImportedModuleAndRejectsFutureOperationsAsync()
    {
        RecordingJsRuntime js = new();
        js.Module.SetResult("getKeyId", "key-id");
        BrowserIntentCryptoService service = new(js, NullLogger<BrowserIntentCryptoService>.Instance);
        await service.GetKeyIdAsync("private");

        await service.DisposeAsync();

        Assert.AreEqual(1, js.Module.DisposeCount);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => service.GetKeyIdAsync("private"));
    }

    [TestMethod]
    public async Task CreateNonceAsync_InvalidSizeFailsBeforeImportAsync()
    {
        RecordingJsRuntime js = new();
        await using BrowserIntentCryptoService service = new(js, NullLogger<BrowserIntentCryptoService>.Instance);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => service.CreateNonceAsync(0));

        Assert.AreEqual(0, js.ImportCount);
    }
}
