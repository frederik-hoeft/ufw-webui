using System.Collections.Immutable;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal sealed class RequestResponsePipeline : IRequestResponsePipeline
{
    private readonly IRequestMiddleware _middlewarePipeline;

    public RequestResponsePipeline(IEnumerable<IRequestMiddleware> requestMiddlewares)
    {
        ImmutableArray<IRequestMiddleware> middlewares = requestMiddlewares.CreatePipeline();
        if (middlewares.IsDefaultOrEmpty)
        {
            throw new ArgumentException("At least one middleware must be provided to create a request-response pipeline.", nameof(requestMiddlewares));
        }
        IRequestMiddleware previous = _middlewarePipeline = middlewares[0];
        for (int i = 1; i < middlewares.Length; i++)
        {
            IRequestMiddleware current = middlewares[i];
            previous.Initialize(current);
            previous = current;
        }
    }

    public ValueTask<IMessage> ProcessMessageAsync(IMessage request, CancellationToken cancellationToken) =>
        _middlewarePipeline.InvokeAsync(request, cancellationToken);
}