using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport;
using Ufw.Ipc.Shared.Transport.Security;
using Ufw.Systemd.Api.Middleware;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Transport;

namespace Ufw.Systemd.Network;

internal sealed class NetworkApplicationWorker
(
    ITransportLayerService transportLayerService,
    ITransportSecurityService transportSecurityService,
    IMessageSerializer messageSerializer,
    IRequestResponsePipeline requestResponsePipeline,
    IConfiguration configuration,
    ILogger logger
) : INetworkApplicationWorker
{
    private readonly Guid _workerId = Guid.CreateVersion7();

    public async Task ServeAsync(INetworkApplication manager, CancellationToken cancellationToken)
    {
        logger.Scoped(this).LogInformation($"Worker {_workerId}: started");
        TimeSpan timeout = configuration.Settings.Network.RequestTimeout;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using ITransportLayerConnection connection = await transportLayerService.ServeAsync(cancellationToken);
                await using Stream networkStream = connection.GetStream(readTimeout: timeout, writeTimeout: timeout);
                await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(networkStream, cancellationToken);
                await using IMessage requestEnvelope = await messageSerializer.ReadAsync(secureStream, cancellationToken);
                await using IMessage responseEnvelope = await requestResponsePipeline.ProcessMessageAsync(requestEnvelope, cancellationToken);
                await messageSerializer.WriteAsync(secureStream, responseEnvelope, cancellationToken);
                await secureStream.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException oce)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                logger.Scoped(this).LogWarning($"Worker {_workerId}: request timed out: {oce.Message}");
            }
        }
        logger.Scoped(this).LogInformation($"Worker {_workerId}: stopping");
    }
}