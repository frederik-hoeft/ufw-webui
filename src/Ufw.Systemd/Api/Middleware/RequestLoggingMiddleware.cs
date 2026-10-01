using System.Diagnostics;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Api.Middleware;

internal sealed class RequestLoggingMiddleware(ILogger logger) : IRequestMiddleware
{
    private readonly ILogger<RequestLoggingMiddleware> _logger = logger.Scoped<RequestLoggingMiddleware>();

    // run before most other middleware to log all incoming requests
    public int Priority => -1;

    public async ValueTask<IResponseMessage> InvokeAsync(IRequestMessage request, RequestMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Request starting: {request.Method} '{request.Route}' ...");
        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            IResponseMessage response = await next(request, cancellationToken);
            _logger.LogInformation($"Request completed: {request.Method} '{request.Route}' - {response.StatusCode} in {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds} ms.");
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation($"Request canceled: {request.Method} '{request.Route}' after {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds} ms.");
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, $"Request failed: {request.Method} '{request.Route}' after {Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds} ms.");
            throw;
        }
    }
}
