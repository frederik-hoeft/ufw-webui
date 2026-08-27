using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport;
using Ufw.Ipc.Shared.Transport.Security;
using Ufw.Systemd.Api.Middleware;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Network;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Transport;

namespace Ufw.Ipc.Tests.Adapter.Hosting;

/// <summary>
/// Opt-in worker for tests that need serving to continue after a connection-level protocol failure.
/// The default test host uses <c>NetworkApplicationWorker</c> so production failure behavior remains observable.
/// </summary>
internal sealed class ResilientIpcTestServerWorker
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
        logger.Scoped(this).LogInformation($"Test worker {_workerId}: started");
        TimeSpan timeout = configuration.Settings.Network.RequestTimeout;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using ITransportLayerConnection connection = await transportLayerService.ServeAsync(cancellationToken).ConfigureAwait(false);
                await using Stream networkStream = connection.GetStream(readTimeout: timeout, writeTimeout: timeout);
                await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(networkStream, cancellationToken).ConfigureAwait(false);
                await using IMessage requestEnvelope = await messageSerializer.ReadAsync(secureStream, cancellationToken).ConfigureAwait(false);
                await using IMessage responseEnvelope = await requestResponsePipeline.ProcessMessageAsync(requestEnvelope, cancellationToken).ConfigureAwait(false);
                await messageSerializer.WriteAsync(secureStream, responseEnvelope, cancellationToken).ConfigureAwait(false);
                await secureStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException oce)
            {
                logger.Scoped(this).LogWarning(oce, $"Test worker {_workerId}: request timed out or was canceled.");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException)
            {
                // Malformed framing / abrupt client disconnects are expected in protocol tests.
                logger.Scoped(this).LogWarning(ex, $"Test worker {_workerId}: connection-level failure.");
            }
        }

        logger.Scoped(this).LogInformation($"Test worker {_workerId}: stopping");
    }
}
