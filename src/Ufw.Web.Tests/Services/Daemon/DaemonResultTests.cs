using Ufw.Ipc.Client;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Tests.Services.Daemon;

[TestClass]
public sealed class DaemonResultTests
{
    [TestMethod]
    public void Success_ExposesResultThroughThrowingAndTryApis()
    {
        object expected = new();
        DaemonResult<object> result = DaemonResult.Success(expected);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Error);
        Assert.AreSame(expected, result.Result);
        Assert.AreSame(result, result.EnsureSuccess());
        Assert.IsTrue(result.TryGetResult(out object? actual, out UfwIpcError? error));
        Assert.AreSame(expected, actual);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void Failure_ExposesErrorWithoutThrowingThroughTryApiAndThrowsThroughAssertiveApis()
    {
        UfwIpcError expected = new(409, "conflict");
        DaemonResult<object> result = DaemonResult.Failure<object>(expected);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSame(expected, result.Error);
        Assert.IsFalse(result.TryGetResult(out object? actual, out UfwIpcError? error));
        Assert.IsNull(actual);
        Assert.AreSame(expected, error);
        UfwIpcException resultException = Assert.ThrowsExactly<UfwIpcException>(() => _ = result.Result);
        UfwIpcException ensureException = Assert.ThrowsExactly<UfwIpcException>(() => result.EnsureSuccess());
        Assert.AreSame(expected, resultException.Error);
        Assert.AreSame(expected, ensureException.Error);
    }

    [TestMethod]
    public void PayloadlessFailure_EnsureSuccessThrowsDaemonError()
    {
        UfwIpcError expected = new(503, "unavailable");
        DaemonResult result = DaemonResult.Failure(expected);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSame(expected, result.Error);
        UfwIpcException exception = Assert.ThrowsExactly<UfwIpcException>(() => result.EnsureSuccess());
        Assert.AreSame(expected, exception.Error);
    }

    [TestMethod]
    public async Task FromIpcAsync_InvalidIpcResponseBecomesDaemonInvalidResponseAsync()
    {
        UfwIpcInvalidResponseException source = new("Malformed daemon response.");

        DaemonInvalidResponseException exception = await Assert.ThrowsExactlyAsync<DaemonInvalidResponseException>(
            () => DaemonResult.FromIpcAsync<string>(() => Task.FromException<UfwIpcResult<string>>(source)));

        Assert.AreSame(source, exception.InnerException);
        Assert.AreEqual(source.Message, exception.Message);
    }
}
