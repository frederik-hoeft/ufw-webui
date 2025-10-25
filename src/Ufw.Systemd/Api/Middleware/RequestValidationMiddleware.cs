using System.Collections.Concurrent;
using System.Diagnostics;
using Ufw.Pipes.Shared.Model;
using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Serialization;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Api.Middleware;

internal sealed class RequestValidationMiddleware(IMessageSerializer messageSerializer) : RequestMiddlewareBase
{
    public override int Priority => short.MinValue;

    public async override ValueTask<IMessage> InvokeAsync(IMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Id) && !string.IsNullOrEmpty(request.Method))
        {
            // normal case, method and route are present, continue processing
            return await Next.InvokeAsync(request, cancellationToken);
        }
        // malformed request, must consume request body before responding
        _ = await request.Payload.TryReadAsync(Timeout.InfiniteTimeSpan, cancellationToken);
        BadRequestResponse badRequest = new($"Malformed request: Missing required fields '{nameof(request.Id)}' or '{nameof(request.Method)}'.");
        IMessage responseMessage = await messageSerializer.SerializeAsync(badRequest, cancellationToken);
        return responseMessage;
    }
}

internal sealed class RequestLoggingMiddleware(IConfiguration configuration, ILogger logger) : RequestMiddlewareBase
{
    private readonly ConcurrentBag<Stopwatch> _stopwatches = [];

    // run before most other middleware to log all incoming requests
    public override int Priority => -1;

    private Stopwatch AcquireStopwatch()
    {
        if (_stopwatches.TryTake(out Stopwatch? stopwatch))
        {
            return stopwatch;
        }
        return new Stopwatch();
    }

    private void ReleaseStopwatch(Stopwatch stopwatch)
    {
        if (_stopwatches.Count <= configuration.Settings.Network.MaxConcurrentConnections)
        {
            stopwatch.Reset();
            _stopwatches.Add(stopwatch);
        }
    }

    public async override ValueTask<IMessage> InvokeAsync(IMessage request, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = AcquireStopwatch();
        logger.Scoped(this).LogInformation($"Request starting: {request.Method} '{request.Id}' ...");
        stopwatch.Start();
        IMessage response = await Next.InvokeAsync(request, cancellationToken);
        stopwatch.Stop();
        logger.Scoped(this).LogInformation($"Request completed: {request.Method} '{request.Id}' - {response.Id} in {stopwatch.Elapsed.TotalMilliseconds} ms.");
        ReleaseStopwatch(stopwatch);
        return response;
    }
}