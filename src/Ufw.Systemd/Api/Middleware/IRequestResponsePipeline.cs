using Ufw.Pipes.Shared.Pipelines;
using Ufw.Pipes.Shared.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal interface IRequestResponsePipeline
{
    ValueTask<IMessage> ProcessMessageAsync(IMessage request, CancellationToken cancellationToken);
}

internal interface IRequestMiddleware : IPipelineHandler
{
    void Initialize(IRequestMiddleware next);

    ValueTask<IMessage> InvokeAsync(IMessage request, CancellationToken cancellationToken);
}