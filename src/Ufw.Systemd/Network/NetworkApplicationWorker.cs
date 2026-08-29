using System.Net.Sockets;
using System.Security.Authentication;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport;
using Ufw.Ipc.Shared.Transport.Itp;
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
    ItpOptions itpOptions,
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
                await ProcessConnectionAsync(secureStream, cancellationToken);
            }
            catch (OperationCanceledException oce)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                logger.Scoped(this).LogWarning($"Worker {_workerId}: request timed out: {oce.Message}");
            }
            catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException or AuthenticationException or TimeoutException)
            {
                logger.Scoped(this).LogWarning(ex, $"Worker {_workerId}: connection failed; continuing to serve requests.");
            }
        }
        logger.Scoped(this).LogInformation($"Worker {_workerId}: stopping");
    }

    private async Task ProcessConnectionAsync(Stream secureStream, CancellationToken cancellationToken)
    {
        ItpConnection itp = new(secureStream, itpOptions);
        ItpFrame frame;
        try
        {
            frame = await itp.ReadAsync(cancellationToken);
        }
        catch (ItpException ex) when (!ex.IsPeerReported)
        {
            logger.Scoped(this).LogWarning(ex, $"Worker {_workerId}: ITP framing failure {ex.ErrorCode}.");
            if (ex.ErrorCode is not ItpErrorCode.InvalidMagic and not ItpErrorCode.VersionMismatch)
            {
                await ItpConnection.TryWriteTransportErrorAsync(
                    secureStream,
                    itpOptions,
                    ex.ErrorCode,
                    ex.Message,
                    cancellationToken);
            }
            return;
        }

        IMessage decoded;
        try
        {
            decoded = messageSerializer.Decode(frame.Payload);
        }
        catch (ApplicationProtocolException ex)
        {
            logger.Scoped(this).LogWarning(ex, $"Worker {_workerId}: application protocol error {ex.Error}.");
            await using IResponseMessage badRequest = await messageSerializer.SerializeResponseAsync(
                new BadRequestResponse(ex.Message),
                cancellationToken);
            await itp.WriteApplicationDataAsync(messageSerializer.Encode(badRequest), cancellationToken);
            return;
        }

        await using (decoded)
        {
            if (decoded is not IRequestMessage request)
            {
                await using IResponseMessage badRequest = await messageSerializer.SerializeResponseAsync(
                    new BadRequestResponse("Expected an application request document."),
                    cancellationToken);
                await itp.WriteApplicationDataAsync(messageSerializer.Encode(badRequest), cancellationToken);
                return;
            }

            await using IResponseMessage response = await requestResponsePipeline.ProcessMessageAsync(request, cancellationToken);
            await itp.WriteApplicationDataAsync(messageSerializer.Encode(response), cancellationToken);
        }
    }
}
