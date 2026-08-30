using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping.Delegates;

namespace Ufw.Systemd.Api.Framework;

internal sealed record UfwEndpointMapping<TRequest, TResponse>(string Method, string Route, int Priority, EndpointInvocationTask<TRequest, TResponse> InvokeEndpointAsync)
    : UfwEndpointMappingBase(Method, Route, Priority)
    where TResponse : IIdentifiable
{
    public async override ValueTask<IResponseMessage> InvokeAsync(IServiceProvider serviceProvider, IRequestMessage request, CancellationToken cancellationToken)
    {
        IMessageSerializer messageSerializer = serviceProvider.GetRequiredService<IMessageSerializer>();
        if (!request.Payload.HasPayload)
        {
            return await BadRequestAsync(messageSerializer, "This endpoint requires a request payload.", cancellationToken);
        }

        TRequest? requestPayload;
        try
        {
            requestPayload = await request.Payload.ReadAsync<TRequest>(cancellationToken);
        }
        catch (ApplicationProtocolException ex)
        {
            return await BadRequestAsync(messageSerializer, ex.Message, cancellationToken);
        }

        if (requestPayload is null)
        {
            return await BadRequestAsync(messageSerializer, "Request payload JSON null cannot be bound to this endpoint.", cancellationToken);
        }

        TResponse responsePayload;
        try
        {
            responsePayload = await InvokeEndpointAsync(serviceProvider, InitializeControllerAsync, requestPayload, cancellationToken);
        }
        catch (Exception e)
        {
            return await messageSerializer.SerializeResponseAsync(InternalServerError(e, serviceProvider), cancellationToken);
        }
        return await messageSerializer.SerializeResponseAsync(responsePayload, cancellationToken);
    }
}
