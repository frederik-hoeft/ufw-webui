using Ufw.Shared.Ipc.Pipelines;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal interface IRequestResponsePipeline
{
    ValueTask<IResponseMessage> ProcessMessageAsync(IRequestMessage request, CancellationToken cancellationToken);
}

internal interface IRequestMiddleware : IPipelineHandler
{
    void Initialize(IRequestMiddleware next);

    ValueTask<IResponseMessage> InvokeAsync(IRequestMessage request, CancellationToken cancellationToken);
}
