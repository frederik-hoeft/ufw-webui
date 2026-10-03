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
        Assert.IsTrue(result.TryGetResult(out object? actual, out UfwIpcException? error));
        Assert.AreSame(expected, actual);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void Failure_ExposesErrorWithoutThrowingThroughTryApiAndThrowsThroughAssertiveApis()
    {
        UfwIpcException expected = new(409, "conflict");
        DaemonResult<object> result = DaemonResult.Failure<object>(expected);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSame(expected, result.Error);
        Assert.IsFalse(result.TryGetResult(out object? actual, out UfwIpcException? error));
        Assert.IsNull(actual);
        Assert.AreSame(expected, error);
        Assert.AreSame(expected, Assert.ThrowsExactly<UfwIpcException>(() => _ = result.Result));
        Assert.AreSame(expected, Assert.ThrowsExactly<UfwIpcException>(() => result.EnsureSuccess()));
    }

    [TestMethod]
    public void PayloadlessFailure_EnsureSuccessThrowsOriginalDaemonError()
    {
        UfwIpcException expected = new(503, "unavailable");
        DaemonResult result = DaemonResult.Failure(expected);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSame(expected, result.Error);
        Assert.AreSame(expected, Assert.ThrowsExactly<UfwIpcException>(() => result.EnsureSuccess()));
    }
}
