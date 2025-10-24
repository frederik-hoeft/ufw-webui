using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Serialization;

namespace Ufw.Systemd.Api.Endpoints;

internal sealed record UnsupportedMethodEndpoint : UfwErrorEndpointBase
{
    protected override ErrorResponse GetErrorResponse(IServiceProvider serviceProvider, IMessage request) => 
        new NotImplementedResponse($"The request method '{request.Method}' is not supported.");
}
