using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Network;

internal sealed class NetworkApplication(IConfiguration configuration, INetworkApplicationWorker worker, ILogger logger) : INetworkApplication
{
    private readonly int _maxWorkers = configuration.Settings.Network.MaxConnections;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.Scoped(this).LogInformation($"Starting network application with {_maxWorkers} workers");
        using CancellationTokenSource workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task[] workerTasks = new Task[_maxWorkers];
        for (int i = 0; i < _maxWorkers; i++)
        {
            workerTasks[i] = worker.ServeAsync(workerCancellation.Token);
        }

        Task firstCompletedWorker = await Task.WhenAny(workerTasks);
        Exception? unexpectedFailure = await GetUnexpectedFailureAsync(firstCompletedWorker, cancellationToken);
        await workerCancellation.CancelAsync();

        try
        {
            await Task.WhenAll(workerTasks);
        }
        catch when (unexpectedFailure is not null)
        {
            // Preserve the worker failure that caused pool shutdown rather than a secondary failure from a sibling observing cancellation.
        }

        if (unexpectedFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(unexpectedFailure).Throw();
        }

        logger.Scoped(this).LogInformation("Network application stopped");
    }

    private static async Task<Exception?> GetUnexpectedFailureAsync(Task completedWorker, CancellationToken applicationCancellationToken)
    {
        if (completedWorker.IsFaulted)
        {
            try
            {
                await completedWorker;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        if (applicationCancellationToken.IsCancellationRequested)
        {
            return null;
        }

        return new InvalidOperationException("A network worker stopped while the daemon was still running.");
    }
}
