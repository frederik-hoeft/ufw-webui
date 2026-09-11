using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Api.Framework;

internal sealed class ApiExceptionMapper(IConfiguration configuration, ILogger logger) : IApiExceptionMapper
{
    private readonly ILogger<ApiExceptionMapper> _logger = logger.Scoped<ApiExceptionMapper>();

    public InternalServerErrorResponse Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _logger.LogError(exception, "An unexpected error occurred while processing an API endpoint.");
        return configuration.Settings.DebugMode
            ? new InternalServerErrorResponse($"An unexpected error occurred while processing the request: {exception}")
            : new InternalServerErrorResponse("An unexpected error occurred while processing the request.");
    }
}
