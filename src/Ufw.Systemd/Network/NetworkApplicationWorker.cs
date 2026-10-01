using System.Net.Sockets;
using System.Security.Authentication;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Transport;

namespace Ufw.Systemd.Network;

internal sealed class NetworkApplicationWorker(ITransportLayerService transportLayerService, INetworkConnectionProcessor connectionProcessor, ILogger logger) : INetworkApplicationWorker
{
    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        Guid workerId = Guid.CreateVersion7();
        logger.Scoped(this).LogInformation($"Worker {workerId}: started");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using Shared.Ipc.Transport.ITransportLayerConnection connection = await transportLayerService.ServeAsync(cancellationToken);
                await connectionProcessor.ProcessAsync(connection, workerId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (IsExpectedConnectionFailure(exception))
            {
                LogConnectionFailure(workerId, exception);
            }
        }
        logger.Scoped(this).LogInformation($"Worker {workerId}: stopping");
    }

    private void LogConnectionFailure(Guid workerId, Exception exception) =>
        logger.Scoped(this).LogWarning(exception, $"Worker {workerId}: connection failed; continuing to serve requests.");

    private static bool IsExpectedConnectionFailure(Exception exception) =>
        exception is OperationCanceledException or SocketException or InvalidDataException or AuthenticationException or TimeoutException or IOException;
}
