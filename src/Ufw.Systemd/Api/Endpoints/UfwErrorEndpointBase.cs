using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Systemd.Api.Framework;

namespace Ufw.Systemd.Api.Endpoints;

internal abstract record UfwErrorEndpointBase() : UfwEndpointMappingBase(Method: "*", Route: "*", Priority: -1)
{
    protected abstract ErrorResponse GetErrorResponse(IServiceProvider serviceProvider, IRequestMessage request);

    public override ValueTask<IResponseMessage> InvokeAsync(IServiceProvider serviceProvider, IRequestMessage request, CancellationToken cancellationToken)
    {
        IMessageSerializer messageSerializer = serviceProvider.GetRequiredService<IMessageSerializer>();
        ErrorResponse errorResponse = GetErrorResponse(serviceProvider, request);
        return messageSerializer.SerializeResponseAsync(errorResponse, cancellationToken);
    }
}
