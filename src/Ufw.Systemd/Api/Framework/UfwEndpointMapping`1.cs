using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping.Delegates;

namespace Ufw.Systemd.Api.Framework;

internal sealed record UfwEndpointMapping<TResponse>(string Method, string Route, int Priority, EndpointInvocationTask<TResponse> InvokeEndpointAsync)
    : UfwEndpointMappingBase(Method, Route, Priority)
    where TResponse : IIdentifiable
{
    public async override ValueTask<IResponseMessage> InvokeAsync(IServiceProvider serviceProvider, IRequestMessage request, CancellationToken cancellationToken)
    {
        IMessageSerializer messageSerializer = serviceProvider.GetRequiredService<IMessageSerializer>();
        if (request.Payload.HasPayload)
        {
            return await BadRequestAsync(messageSerializer, "This endpoint does not accept a request payload.", cancellationToken);
        }

        TResponse responsePayload;
        try
        {
            responsePayload = await InvokeEndpointAsync(serviceProvider, InitializeControllerAsync, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return await messageSerializer.SerializeResponseAsync(InternalServerError(e, serviceProvider), cancellationToken);
        }
        return await messageSerializer.SerializeResponseAsync(responsePayload, cancellationToken);
    }
}
