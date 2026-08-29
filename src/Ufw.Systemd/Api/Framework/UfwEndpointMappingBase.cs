using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Api.Framework;

internal abstract record UfwEndpointMappingBase(string Method, string Route, int Priority) : ApiEndpointMapping<IRequestMessage, IResponseMessage>(Method, Route, Priority)
{
    protected static InternalServerErrorResponse InternalServerError(Exception exception, IServiceProvider serviceProvider)
    {
        IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
        if (configuration.Settings.DebugMode)
        {
            return new InternalServerErrorResponse($"An unexpected error occurred while processing the request: {exception}");
        }
        return new InternalServerErrorResponse("An unexpected error occurred while processing the request.");
    }

    protected static ValueTask InitializeControllerAsync(IServiceProvider serviceProvider, ControllerBase controller, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
