using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Api.Framework;

internal abstract record UfwEndpointMappingBase(string Method, string Route, int Priority) : ApiEndpointMapping<IRequestMessage, IResponseMessage>(Method, Route, Priority)
{
    protected static ValueTask<IResponseMessage> BadRequestAsync(IMessageSerializer messageSerializer, string message, CancellationToken cancellationToken) =>
        messageSerializer.SerializeResponseAsync(new BadRequestResponse(message), cancellationToken);

    protected static InternalServerErrorResponse InternalServerError(Exception exception, IServiceProvider serviceProvider)
    {
        ILogger logger = serviceProvider.GetRequiredService<ILogger>();
        logger.Scoped<UfwEndpointMappingBase>().LogError(exception, "An unexpected error occurred while processing an API endpoint.");

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
