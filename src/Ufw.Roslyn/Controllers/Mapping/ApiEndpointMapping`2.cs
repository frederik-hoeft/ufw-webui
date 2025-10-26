using Ufw.Roslyn.Controllers.Routing;

namespace Ufw.Roslyn.Controllers.Mapping;

public abstract record ApiEndpointMapping<TRequestEnvelope, TResponseEnvelope>(string Method, string Route, int Priority)
    : IApiEndpoint<TRequestEnvelope, TResponseEnvelope>, IRoutingSegment
{
    public abstract ValueTask<TResponseEnvelope> InvokeAsync(IServiceProvider serviceProvider, TRequestEnvelope request, CancellationToken cancellationToken);
}
