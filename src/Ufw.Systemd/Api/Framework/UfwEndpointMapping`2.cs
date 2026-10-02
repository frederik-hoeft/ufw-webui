using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping.Delegates;
using Ufw.Shared.Ipc.Protocol;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Framework;

internal sealed record UfwEndpointMapping<TRequest, TResponse>(string Method, string Route, int Priority, EndpointInvocationTask<TRequest, TResponse> InvokeEndpointAsync)
    : UfwEndpointMappingBase(Method, Route, Priority)
    where TResponse : IIdentifiable
{
    public async override ValueTask<IResponseMessage> InvokeAsync(IServiceProvider serviceProvider, IRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Payload.HasPayload)
        {
            return await BadRequestAsync(serviceProvider, "This endpoint requires a request payload.", cancellationToken);
        }

        TRequest? requestPayload;
        try
        {
            requestPayload = await request.Payload.ReadAsync<TRequest>(cancellationToken);
        }
        catch (ApplicationProtocolException exception)
        {
            return await BadRequestAsync(serviceProvider, exception.Message, cancellationToken);
        }

        if (requestPayload is null)
        {
            return await BadRequestAsync(serviceProvider, "Request payload JSON null cannot be bound to this endpoint.", cancellationToken);
        }

        return await InvokeAndSerializeAsync(serviceProvider, cancellationToken => InvokeEndpointAsync(serviceProvider, requestPayload, cancellationToken), cancellationToken);
    }
}
