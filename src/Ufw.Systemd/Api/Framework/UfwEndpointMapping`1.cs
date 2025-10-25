using Microsoft.Extensions.DependencyInjection;
using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping.Delegates;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Api.Framework;

internal sealed record UfwEndpointMapping<TResponse>(string Method, string Route, int Priority, EndpointInvocationTask<TResponse> InvokeEndpointAsync)
    : UfwEndpointMappingBase(Method, Route, Priority)
    where TResponse : IIdentifiable
{
    public async override ValueTask<IMessage> InvokeAsync(IServiceProvider serviceProvider, IMessage request, CancellationToken cancellationToken)
    {
        IMessageSerializer messageSerializer = serviceProvider.GetRequiredService<IMessageSerializer>();
        IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
        bool success = await request.Payload.TryReadAsync(configuration.Settings.Network.RequestTimeout, cancellationToken);
        if (!success)
        {
            RequestTimeoutResponse timeout = new("The request payload could not be read within the specified timeout period.");
            return await messageSerializer.SerializeAsync(timeout, cancellationToken);
        }
        TResponse responsePayload;
        try
        {
            responsePayload = await InvokeEndpointAsync(serviceProvider, InitializeControllerAsync, cancellationToken);
        }
        catch (Exception e)
        {
            return await messageSerializer.SerializeAsync(InternalServerError(e, serviceProvider), cancellationToken);
        }
        return await messageSerializer.SerializeAsync(responsePayload, cancellationToken);
    }
}
