using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

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