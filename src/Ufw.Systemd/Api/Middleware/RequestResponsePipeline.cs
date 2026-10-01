using System.Collections.Immutable;
using Ufw.Shared.Ipc.Pipelines;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal sealed class RequestResponsePipeline : IRequestResponsePipeline
{
    private readonly RequestMiddlewareDelegate _pipeline;

    public RequestResponsePipeline(IEnumerable<IRequestMiddleware> requestMiddlewares)
    {
        ImmutableArray<IRequestMiddleware> middlewares = requestMiddlewares.CreatePipeline();
        if (middlewares.IsDefaultOrEmpty)
        {
            throw new ArgumentException("At least one middleware must be provided to create a request-response pipeline.", nameof(requestMiddlewares));
        }

        RequestMiddlewareDelegate pipeline = static (_, _) => throw new InvalidOperationException("The request middleware pipeline completed without producing a response.");
        for (int i = middlewares.Length - 1; i >= 0; --i)
        {
            IRequestMiddleware middleware = middlewares[i];
            RequestMiddlewareDelegate next = pipeline;
            pipeline = (request, cancellationToken) => middleware.InvokeAsync(request, next, cancellationToken);
        }
        _pipeline = pipeline;
    }

    public ValueTask<IResponseMessage> ProcessMessageAsync(IRequestMessage request, CancellationToken cancellationToken) => _pipeline(request, cancellationToken);
}
