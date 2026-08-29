using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Systemd.Api.Endpoints;

internal sealed record NotFoundEndpoint : UfwErrorEndpointBase
{
    private static readonly NotFoundResponse s_notFoundResponse = new("The requested resource was not found.");

    protected override ErrorResponse GetErrorResponse(IServiceProvider serviceProvider, IRequestMessage request) => s_notFoundResponse;
}
