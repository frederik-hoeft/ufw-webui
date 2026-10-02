using Moq;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Systemd.Api.Middleware;

namespace Ufw.Systemd.Tests.Api.Middleware;

[TestClass]
public sealed class RequestResponsePipelineTests
{
    [TestMethod]
    public async Task ProcessMessageAsync_ComposesInPriorityOrderWithoutMutatingMiddlewareInstances()
    {
        List<string> invocations = [];
        Mock<IResponseMessage> response = new();
        RecordingMiddleware outer = new(priority: -10, "outer", invocations);
        RecordingMiddleware inner = new(priority: 10, "inner", invocations);
        TerminalMiddleware terminal = new(priority: 100, response.Object, invocations);
        IRequestMiddleware[] middlewares = [inner, terminal, outer];
        RequestResponsePipeline firstPipeline = new(middlewares);
        RequestResponsePipeline secondPipeline = new(middlewares);
        Mock<IRequestMessage> request = new();

        IResponseMessage firstResponse = await firstPipeline.ProcessMessageAsync(request.Object, CancellationToken.None);
        IResponseMessage secondResponse = await secondPipeline.ProcessMessageAsync(request.Object, CancellationToken.None);

        Assert.AreSame(response.Object, firstResponse);
        Assert.AreSame(response.Object, secondResponse);
        string[] expectedInvocations = ["outer:before", "inner:before", "terminal", "inner:after", "outer:after", "outer:before", "inner:before", "terminal", "inner:after", "outer:after"];
        CollectionAssert.AreEqual(expectedInvocations, invocations);
    }

    private sealed class RecordingMiddleware(int priority, string name, List<string> invocations) : IRequestMiddleware
    {
        public int Priority => priority;

        public async ValueTask<IResponseMessage> InvokeAsync(IRequestMessage request, RequestMiddlewareDelegate next, CancellationToken cancellationToken)
        {
            invocations.Add($"{name}:before");
            IResponseMessage response = await next(request, cancellationToken);
            invocations.Add($"{name}:after");
            return response;
        }
    }

    private sealed class TerminalMiddleware(int priority, IResponseMessage response, List<string> invocations) : IRequestMiddleware
    {
        public int Priority => priority;

        public ValueTask<IResponseMessage> InvokeAsync(IRequestMessage request, RequestMiddlewareDelegate next, CancellationToken cancellationToken)
        {
            invocations.Add("terminal");
            return ValueTask.FromResult(response);
        }
    }
}
