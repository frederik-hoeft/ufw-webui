using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers.Mapping;

namespace Ufw.Systemd.Api.Middleware;

internal sealed class EndpointInvocationMiddleware(IServiceProvider serviceProvider, IApiEndpointMap<IRequestMessage, IResponseMessage> endpointMap) : RequestMiddlewareBase
{
    public override int Priority => int.MaxValue - 1;

    public async override ValueTask<IResponseMessage> InvokeAsync(IRequestMessage request, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IApiEndpoint<IRequestMessage, IResponseMessage> endpoint = endpointMap.Match(request.Method, request.Route);
        return await endpoint.InvokeAsync(scope.ServiceProvider, request, cancellationToken);
    }
}
