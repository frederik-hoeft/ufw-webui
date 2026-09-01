using System.Collections.Concurrent;
using System.Diagnostics;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Api.Middleware;

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
        if (_stopwatches.Count <= configuration.Settings.Network.MaxConnections)
        {
            stopwatch.Reset();
            _stopwatches.Add(stopwatch);
        }
    }

    public async override ValueTask<IResponseMessage> InvokeAsync(IRequestMessage request, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = AcquireStopwatch();
        logger.Scoped(this).LogInformation($"Request starting: {request.Method} '{request.Route}' ...");
        stopwatch.Start();
        IResponseMessage response = await Next.InvokeAsync(request, cancellationToken);
        stopwatch.Stop();
        logger.Scoped(this).LogInformation($"Request completed: {request.Method} '{request.Route}' - {response.StatusCode} in {stopwatch.Elapsed.TotalMilliseconds} ms.");
        ReleaseStopwatch(stopwatch);
        return response;
    }
}
