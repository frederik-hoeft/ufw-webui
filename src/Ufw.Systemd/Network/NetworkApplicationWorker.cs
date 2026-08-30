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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using ITransportLayerConnection connection = await transportLayerService.ServeAsync(cancellationToken);
                await ProcessAcceptedConnectionAsync(connection, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException ex)
            {
                LogConnectionFailure(ex);
            }
            catch (SocketException ex)
            {
                LogConnectionFailure(ex);
            }
            catch (InvalidDataException ex)
            {
                LogConnectionFailure(ex);
            }
            catch (AuthenticationException ex)
            {
                LogConnectionFailure(ex);
            }
            catch (TimeoutException ex)
            {
                LogConnectionFailure(ex);
            }
            catch (IOException ex)
            {
                LogConnectionFailure(ex);
            }
        }
        logger.Scoped(this).LogInformation($"Worker {_workerId}: stopping");
    }

    private async Task ProcessAcceptedConnectionAsync(ITransportLayerConnection connection, CancellationToken cancellationToken)
    {
        TimeSpan requestTimeout = configuration.Settings.Network.RequestTimeout;
        using CancellationTokenSource? requestTimeoutSource = CreateRequestTimeoutSource(requestTimeout, cancellationToken);
        CancellationToken requestToken = requestTimeoutSource?.Token ?? cancellationToken;

        try
        {
            TimeSpan ioTimeout = configuration.Settings.Network.IoTimeout;
            await using Stream networkStream = connection.GetStream(readTimeout: ioTimeout, writeTimeout: ioTimeout);
            await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(networkStream, requestToken);
            await ProcessConnectionAsync(secureStream, requestToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && requestTimeoutSource?.IsCancellationRequested == true)
        {
            throw new TimeoutException("The IPC transaction exceeded the configured request timeout.", ex);
        }
    }

    private async Task ProcessConnectionAsync(Stream secureStream, CancellationToken cancellationToken)
    {
        ItpConnection itp = new(secureStream, itpOptions);
        ItpFrame frame;
        try
        {
            frame = await itp.ReadAsync(cancellationToken);
        }
        catch (ItpException ex) when (ex.IsPeerReported)
        {
            logger.Scoped(this).LogWarning(ex, $"Worker {_workerId}: peer reported ITP failure {ex.ErrorCode}.");
            return;
        }
        catch (ItpException ex)
        {
            logger.Scoped(this).LogWarning(ex, $"Worker {_workerId}: ITP framing failure {ex.ErrorCode}.");
            if (ex.CanReplyWithTransportError)
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

    private static CancellationTokenSource? CreateRequestTimeoutSource(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return null;
        }

        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }

    private void LogConnectionFailure(Exception exception) =>
        logger.Scoped(this).LogWarning(exception, $"Worker {_workerId}: connection failed; continuing to serve requests.");
}
