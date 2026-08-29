using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Controllers.Mapping.Delegates;

namespace Ufw.Systemd.Api.Framework;

internal sealed class UfwApiEndpointMappingFactory : IApiEndpointMappingFactory<IRequestMessage, IResponseMessage>
{
    public static ApiEndpointMapping<IRequestMessage, IResponseMessage> Map<TRequest, TResponse>(string method, string route, int priority, EndpointInvocationTask<TRequest, TResponse> invokeAsync)
        where TResponse : IIdentifiable => new UfwEndpointMapping<TRequest, TResponse>(method, route, priority, invokeAsync);

    public static ApiEndpointMapping<IRequestMessage, IResponseMessage> Map<TResponse>(string method, string route, int priority, EndpointInvocationTask<TResponse> invokeAsync)
        where TResponse : IIdentifiable => new UfwEndpointMapping<TResponse>(method, route, priority, invokeAsync);
}
