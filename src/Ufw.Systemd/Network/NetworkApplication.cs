using Microsoft.Extensions.DependencyInjection;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Network;

internal sealed class NetworkApplication(IConfiguration configuration, IServiceProvider serviceProvider, ILogger logger) : INetworkApplication
{
    private readonly int _maxWorkers = configuration.Settings.Network.MaxConnections;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.Scoped(this).LogInformation($"Starting network application with {_maxWorkers} workers");
        List<Task> workerTasks = new(_maxWorkers);
        for (int i = 0; i < _maxWorkers; i++)
        {
            INetworkApplicationWorker worker = serviceProvider.GetRequiredService<INetworkApplicationWorker>();
            Task workerTask = worker.ServeAsync(this, cancellationToken);
            workerTasks.Add(workerTask);
        }
        await Task.WhenAll(workerTasks);
        logger.Scoped(this).LogInformation("Network application stopped");
    }
}
