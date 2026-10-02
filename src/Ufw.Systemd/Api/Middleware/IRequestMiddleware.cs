using Ufw.Shared.Ipc.Pipelines;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal interface IRequestMiddleware : IPipelineHandler
{
    ValueTask<IResponseMessage> InvokeAsync(IRequestMessage request, RequestMiddlewareDelegate next, CancellationToken cancellationToken);
}
