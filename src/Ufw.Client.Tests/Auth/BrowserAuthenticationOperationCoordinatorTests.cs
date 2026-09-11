using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Ufw.Client.Auth;
using Ufw.Client.Errors;
using Ufw.Client.Tests.Support;

namespace Ufw.Client.Tests.Auth;

[TestClass]
public sealed class BrowserAuthenticationOperationCoordinatorTests
{
    [TestMethod]
    public async Task RunExclusiveAsync_AcquiresBrowserLockRunsOperationAndReleasesAsync()
    {
        RecordingJsRuntime js = new();
        await using BrowserAuthenticationOperationCoordinator coordinator = new(js, NullLogger<BrowserAuthenticationOperationCoordinator>.Instance);
        bool operationRan = false;

        int result = await coordinator.RunExclusiveAsync(_ =>
        {
            operationRan = true;
            return Task.FromResult(42);
        });

        Assert.AreEqual(42, result);
        Assert.IsTrue(operationRan);
        Assert.AreEqual(1, js.ImportCount);
        CollectionAssert.AreEqual(new[] { "acquire", "release" }, js.Module.Calls.Select(static call => call.Identifier).ToArray());
        Assert.AreEqual("ufw-webui-auth-session", js.Module.Calls[0].Args[0]);
        Assert.AreEqual(js.Module.Calls[0].Args[1], js.Module.Calls[1].Args[0]);
    }

    [TestMethod]
    public async Task RunExclusiveAsync_OperationFailureStillReleasesBrowserLockAsync()
    {
        RecordingJsRuntime js = new();
        await using BrowserAuthenticationOperationCoordinator coordinator = new(js, NullLogger<BrowserAuthenticationOperationCoordinator>.Instance);
        InvalidOperationException failure = new("operation failed");

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            coordinator.RunExclusiveAsync<int>(_ => Task.FromException<int>(failure)));

        Assert.AreSame(failure, actual);
        CollectionAssert.AreEqual(new[] { "acquire", "release" }, js.Module.Calls.Select(static call => call.Identifier).ToArray());
    }

    [TestMethod]
    public async Task RunExclusiveAsync_BrowserLockFailureIsTranslatedAndOperationDoesNotRunAsync()
    {
        RecordingJsRuntime js = new();
        js.Module.SetException("acquire", new JSException("locks unavailable"));
        await using BrowserAuthenticationOperationCoordinator coordinator = new(js, NullLogger<BrowserAuthenticationOperationCoordinator>.Instance);
        bool operationRan = false;

        BrowserOperationException exception = await Assert.ThrowsExactlyAsync<BrowserOperationException>(() =>
            coordinator.RunExclusiveAsync(_ =>
            {
                operationRan = true;
                return Task.CompletedTask;
            }));

        Assert.IsInstanceOfType<JSException>(exception.InnerException);
        Assert.IsFalse(operationRan);
        Assert.IsFalse(js.Module.Calls.Any(static call => call.Identifier == "release"));
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesImportedModuleAndRejectsNewOperationsAsync()
    {
        RecordingJsRuntime js = new();
        BrowserAuthenticationOperationCoordinator coordinator = new(js, NullLogger<BrowserAuthenticationOperationCoordinator>.Instance);
        await coordinator.RunExclusiveAsync(_ => Task.CompletedTask);

        await coordinator.DisposeAsync();

        Assert.AreEqual(1, js.Module.DisposeCount);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => coordinator.RunExclusiveAsync(_ => Task.CompletedTask));
    }
}
