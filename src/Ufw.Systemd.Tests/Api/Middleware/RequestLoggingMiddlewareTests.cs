using Moq;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Systemd.Api.Middleware;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Tests.Api.Middleware;

[TestClass]
public sealed class RequestLoggingMiddlewareTests
{
    [TestMethod]
    public async Task InvokeAsync_DownstreamSucceeds_LogsCompletion()
    {
        (RequestLoggingMiddleware middleware, Mock<ILogger<RequestLoggingMiddleware>> logger) = CreateMiddleware();
        Mock<IRequestMessage> request = CreateRequest();
        Mock<IResponseMessage> response = new();
        response.SetupGet(value => value.StatusCode).Returns(200);

        IResponseMessage actual = await middleware.InvokeAsync(request.Object, (_, _) => ValueTask.FromResult(response.Object), CancellationToken.None);

        Assert.AreSame(response.Object, actual);
        logger.Verify(value => value.LogInformation(It.Is<string>(message => message.StartsWith("Request starting:", StringComparison.Ordinal))), Times.Once);
        logger.Verify(value => value.LogInformation(It.Is<string>(message => message.StartsWith("Request completed:", StringComparison.Ordinal))), Times.Once);
    }

    [TestMethod]
    public async Task InvokeAsync_DownstreamCancellation_LogsCancellationAndPropagates()
    {
        (RequestLoggingMiddleware middleware, Mock<ILogger<RequestLoggingMiddleware>> logger) = CreateMiddleware();
        Mock<IRequestMessage> request = CreateRequest();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await middleware.InvokeAsync(request.Object, (_, token) => ValueTask.FromCanceled<IResponseMessage>(token), cancellation.Token));

        logger.Verify(value => value.LogInformation(It.Is<string>(message => message.StartsWith("Request canceled:", StringComparison.Ordinal))), Times.Once);
        logger.Verify(value => value.LogError(It.IsAny<Exception>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task InvokeAsync_DownstreamFailure_LogsFailureAndPropagates()
    {
        (RequestLoggingMiddleware middleware, Mock<ILogger<RequestLoggingMiddleware>> logger) = CreateMiddleware();
        Mock<IRequestMessage> request = CreateRequest();
        InvalidOperationException failure = new("boom");

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await middleware.InvokeAsync(request.Object, (_, _) => ValueTask.FromException<IResponseMessage>(failure), CancellationToken.None));

        Assert.AreSame(failure, actual);
        logger.Verify(value => value.LogError(failure, It.Is<string>(message => message.StartsWith("Request failed:", StringComparison.Ordinal))), Times.Once);
    }

    private static (RequestLoggingMiddleware Middleware, Mock<ILogger<RequestLoggingMiddleware>> Logger) CreateMiddleware()
    {
        Mock<ILogger<RequestLoggingMiddleware>> scopedLogger = new();
        Mock<ILogger> logger = new();
        logger.Setup(value => value.Scoped<RequestLoggingMiddleware>()).Returns(scopedLogger.Object);
        return (new RequestLoggingMiddleware(logger.Object), scopedLogger);
    }

    private static Mock<IRequestMessage> CreateRequest()
    {
        Mock<IRequestMessage> request = new();
        request.SetupGet(value => value.Method).Returns("GET");
        request.SetupGet(value => value.Route).Returns("/test");
        return request;
    }
}
