using Microsoft.Extensions.DependencyInjection;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Middleware;

internal sealed class EndpointInvocationMiddleware(IServiceProvider serviceProvider, IApiEndpointMap<IRequestMessage, IResponseMessage> endpointMap) : IRequestMiddleware
{
    public int Priority => int.MaxValue - 1;

    public async ValueTask<IResponseMessage> InvokeAsync(IRequestMessage request, RequestMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IApiEndpoint<IRequestMessage, IResponseMessage> endpoint = endpointMap.Match(request.Method, request.Route);
        return await endpoint.InvokeAsync(scope.ServiceProvider, request, cancellationToken);
    }
}
