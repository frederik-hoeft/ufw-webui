using Microsoft.Extensions.DependencyInjection;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Network;

internal sealed class NetworkApplication(IConfiguration configuration, IServiceProvider serviceProvider) : INetworkApplication
{
    private readonly int _maxWorkers = configuration.Settings.Network.MaxConcurrentConnections;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        List<Task> workerTasks = new(_maxWorkers);
        for (int i = 0; i < _maxWorkers; i++)
        {
            INetworkApplicationWorker worker = serviceProvider.GetRequiredService<INetworkApplicationWorker>();
            Task workerTask = worker.ServeAsync(this, cancellationToken);
            workerTasks.Add(workerTask);
        }
        return Task.WhenAll(workerTasks);
    }
}
