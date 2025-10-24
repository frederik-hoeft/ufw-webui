using Ufw.Pipes.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Controllers.Mapping.Delegates;

namespace Ufw.Systemd.Api.Framework;

internal sealed class UfwApiEndpointMappingFactory : IApiEndpointMappingFactory<IMessage, IMessage>
{
    public static ApiEndpointMapping<IMessage, IMessage> Map<TRequest, TResponse>(string method, string route, int priority, EndpointInvocationTask<TRequest, TResponse> invokeAsync)
        where TResponse : IIdentifiable => new UfwEndpointMapping<TRequest, TResponse>(method, route, priority, invokeAsync);

    public static ApiEndpointMapping<IMessage, IMessage> Map<TResponse>(string method, string route, int priority, EndpointInvocationTask<TResponse> invokeAsync)
        where TResponse : IIdentifiable => new UfwEndpointMapping<TResponse>(method, route, priority, invokeAsync);
}