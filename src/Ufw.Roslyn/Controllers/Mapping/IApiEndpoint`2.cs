using Ufw.Roslyn.Controllers.Routing;

namespace Ufw.Roslyn.Controllers.Mapping;

public interface IApiEndpoint<in TRequestEnvelope, TResponseEnvelope> : IRoutingSegment
{
    ValueTask<TResponseEnvelope> InvokeAsync(IServiceProvider serviceProvider, TRequestEnvelope request, CancellationToken cancellationToken);
}
