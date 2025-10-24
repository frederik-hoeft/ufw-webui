using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Serialization;

namespace Ufw.Systemd.Api.Endpoints;

internal sealed record NotFoundEndpoint : UfwErrorEndpointBase
{
    private static readonly NotFoundResponse s_notFoundResponse = new("The requested resource was not found.");

    protected override ErrorResponse GetErrorResponse(IServiceProvider serviceProvider, IMessage request) => s_notFoundResponse;
}
