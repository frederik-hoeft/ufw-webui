using Microsoft.Extensions.DependencyInjection;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Systemd.Api.Framework;

internal abstract record UfwEndpointMappingBase(string Method, string Route, int Priority) : ApiEndpointMapping<IRequestMessage, IResponseMessage>(Method, Route, Priority)
{
    protected static ValueTask<IResponseMessage> BadRequestAsync(IMessageSerializer messageSerializer, string message, CancellationToken cancellationToken) =>
        messageSerializer.SerializeResponseAsync(new BadRequestResponse(message), cancellationToken);

    protected static ValueTask InitializeControllerAsync(IServiceProvider serviceProvider, ControllerBase controller, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
