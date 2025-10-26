using Ufw.Roslyn.Controllers.Mapping.Delegates;

namespace Ufw.Roslyn.Controllers.Mapping;

public interface IApiEndpointMappingFactory<TRequestEnvelope, TResponseEnvelope>
{
    static abstract ApiEndpointMapping<TRequestEnvelope, TResponseEnvelope> Map<TRequest, TResponse>(string method, string route, int priority, EndpointInvocationTask<TRequest, TResponse> invokeAsync)
        where TResponse : IIdentifiable;

    static abstract ApiEndpointMapping<TRequestEnvelope, TResponseEnvelope> Map<TResponse>(string method, string route, int priority, EndpointInvocationTask<TResponse> invokeAsync)
        where TResponse : IIdentifiable;
}
