using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping.Delegates;

namespace Ufw.Systemd.Api.Framework;

internal sealed record UfwEndpointMapping<TResponse>(string Method, string Route, int Priority, EndpointInvocationTask<TResponse> InvokeEndpointAsync)
    : UfwEndpointMappingBase(Method, Route, Priority)
    where TResponse : IIdentifiable
{
    public async override ValueTask<IMessage> InvokeAsync(IServiceProvider serviceProvider, IMessage request, CancellationToken cancellationToken)
    {
        IMessageSerializer messageSerializer = serviceProvider.GetRequiredService<IMessageSerializer>();
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
