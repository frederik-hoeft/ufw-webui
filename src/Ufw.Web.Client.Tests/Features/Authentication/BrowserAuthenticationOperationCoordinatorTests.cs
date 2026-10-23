using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Ufw.Web.Client.Features.Authentication;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.Tests.Support;

namespace Ufw.Web.Client.Tests.Features.Authentication;

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
        }, TestContext.CancellationToken);

        Assert.AreEqual(42, result);
        Assert.IsTrue(operationRan);
        Assert.AreEqual(1, js.ImportCount);
        Assert.AreSequenceEqual(["acquire", "release"], js.Module.Calls.Select(static call => call.Identifier).ToArray());
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
            coordinator.RunExclusiveAsync<int>(_ => Task.FromException<int>(failure), TestContext.CancellationToken));

        Assert.AreSame(failure, actual);
        Assert.AreSequenceEqual(["acquire", "release"], js.Module.Calls.Select(static call => call.Identifier).ToArray());
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
            }, TestContext.CancellationToken));

        Assert.IsInstanceOfType<JSException>(exception.InnerException);
        Assert.IsFalse(operationRan);
        Assert.DoesNotContain(static call => call.Identifier == "release", js.Module.Calls);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesImportedModuleAndRejectsNewOperationsAsync()
    {
        RecordingJsRuntime js = new();
        BrowserAuthenticationOperationCoordinator coordinator = new(js, NullLogger<BrowserAuthenticationOperationCoordinator>.Instance);
        await coordinator.RunExclusiveAsync(_ => Task.CompletedTask, TestContext.CancellationToken);

        await coordinator.DisposeAsync();

        Assert.AreEqual(1, js.Module.DisposeCount);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => coordinator.RunExclusiveAsync(_ => Task.CompletedTask, TestContext.CancellationToken));
    }

    public TestContext TestContext { get; set; }
}
