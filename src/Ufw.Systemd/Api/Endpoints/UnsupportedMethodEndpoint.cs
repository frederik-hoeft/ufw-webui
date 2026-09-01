using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Systemd.Api.Endpoints;

internal sealed record UnsupportedMethodEndpoint : UfwErrorEndpointBase
{
    protected override ErrorResponse GetErrorResponse(IServiceProvider serviceProvider, IRequestMessage request) =>
        new NotImplementedResponse($"The request method '{request.Method}' is not supported.");
}
