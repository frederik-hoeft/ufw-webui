using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Endpoints;

internal sealed record UnsupportedMethodEndpoint : UfwErrorEndpointBase
{
    protected override ErrorResponse GetErrorResponse(IServiceProvider serviceProvider, IRequestMessage request) =>
        new NotImplementedResponse($"The request method '{request.Method}' is not supported.");
}
