using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping.Delegates;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Api.Framework;

internal sealed record UfwEndpointMapping<TRequest, TResponse>(string Method, string Route, int Priority, EndpointInvocationTask<TRequest, TResponse> InvokeEndpointAsync)
    : UfwEndpointMappingBase(Method, Route, Priority)
    where TResponse : IIdentifiable
{
    public async override ValueTask<IResponseMessage> InvokeAsync(IServiceProvider serviceProvider, IRequestMessage request, CancellationToken cancellationToken)
    {
        IMessageSerializer messageSerializer = serviceProvider.GetRequiredService<IMessageSerializer>();
        IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
        TRequest? requestPayload = await request.Payload.ReadAsync<TRequest>(cancellationToken);
        if (requestPayload is null)
        {
            BadRequestResponse badRequest = new("Request payload was null or did not match the expected type.");
            return await messageSerializer.SerializeResponseAsync(badRequest, cancellationToken);
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
