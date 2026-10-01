using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping.Delegates;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Framework;

internal sealed record UfwEndpointMapping<TResponse>(string Method, string Route, int Priority, EndpointInvocationTask<TResponse> InvokeEndpointAsync)
    : UfwEndpointMappingBase(Method, Route, Priority)
    where TResponse : IIdentifiable
{
    public override ValueTask<IResponseMessage> InvokeAsync(IServiceProvider serviceProvider, IRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Payload.HasPayload)
        {
            return BadRequestAsync(serviceProvider, "This endpoint does not accept a request payload.", cancellationToken);
        }
        return InvokeAndSerializeAsync(serviceProvider, cancellationToken => InvokeEndpointAsync(serviceProvider, cancellationToken), cancellationToken);
    }
}
