using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers.Mapping;

namespace Ufw.Systemd.Api.Middleware;

internal sealed class EndpointInvocationMiddleware(IServiceProvider serviceProvider, IApiEndpointMap<IMessage, IMessage> endpointMap) : RequestMiddlewareBase
{
    public override int Priority => int.MaxValue - 1;

    public async override ValueTask<IMessage> InvokeAsync(IMessage request, CancellationToken cancellationToken)
    {
        // should never be null here as previous middleware should have validated this
        _ = request.Method ?? throw new InvalidOperationException("Request message does not contain a method.");
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        IApiEndpoint<IMessage, IMessage> endpoint = endpointMap.Match(request.Method, request.Id);
        IMessage response = await endpoint.InvokeAsync(scope.ServiceProvider, request, cancellationToken);
        return response;
    }
}
