using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Protocol;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Shared.Ipc.Transport;
using Ufw.Shared.Ipc.Transport.Itp;
using Ufw.Shared.Ipc.Transport.Security;
using Ufw.Systemd.Api.Middleware;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Network;

internal sealed class NetworkConnectionProcessor(
    ITransportSecurityService transportSecurityService,
    IMessageSerializer messageSerializer,
    IRequestResponsePipeline requestResponsePipeline,
    IConfiguration configuration,
    ItpOptions itpOptions,
    ILogger logger) : INetworkConnectionProcessor
{
    public async Task ProcessAsync(ITransportLayerConnection connection, Guid workerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        TimeSpan requestTimeout = configuration.Settings.Network.RequestTimeout;
        using CancellationTokenSource? requestTimeoutSource = CreateRequestTimeoutSource(requestTimeout, cancellationToken);
        CancellationToken requestToken = requestTimeoutSource?.Token ?? cancellationToken;

        try
        {
            TimeSpan ioTimeout = configuration.Settings.Network.IoTimeout;
            await using Stream networkStream = connection.GetStream(readTimeout: ioTimeout, writeTimeout: ioTimeout);
            await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(networkStream, requestToken);
            await ProcessApplicationRequestAsync(secureStream, workerId, requestToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && requestTimeoutSource?.IsCancellationRequested == true)
        {
            throw new TimeoutException("The IPC transaction exceeded the configured request timeout.", exception);
        }
    }

    private async Task ProcessApplicationRequestAsync(Stream secureStream, Guid workerId, CancellationToken cancellationToken)
    {
        ItpConnection itp = new(secureStream, itpOptions);
        ItpFrame frame;
        try
        {
            frame = await itp.ReadAsync(cancellationToken);
        }
        catch (ItpException exception) when (exception.IsPeerReported)
        {
            logger.Scoped(this).LogWarning(exception, $"Worker {workerId}: peer reported ITP failure {exception.ErrorCode}.");
            return;
        }
        catch (ItpException exception)
        {
            logger.Scoped(this).LogWarning(exception, $"Worker {workerId}: ITP framing failure {exception.ErrorCode}.");
            if (exception.CanReplyWithTransportError)
            {
                await ItpConnection.TryWriteTransportErrorAsync(secureStream, itpOptions, exception.ErrorCode, exception.Message, cancellationToken);
            }
            return;
        }

        IMessage decoded;
        try
        {
            decoded = messageSerializer.Decode(frame.Payload);
        }
        catch (ApplicationProtocolException exception)
        {
            logger.Scoped(this).LogWarning(exception, $"Worker {workerId}: application protocol error {exception.Error}.");
            await using IResponseMessage badRequest = await messageSerializer.SerializeResponseAsync(new BadRequestResponse(exception.Message), cancellationToken);
            await itp.WriteApplicationDataAsync(messageSerializer.Encode(badRequest), cancellationToken);
            return;
        }

        await using (decoded)
        {
            if (decoded is not IRequestMessage request)
            {
                await using IResponseMessage badRequest = await messageSerializer.SerializeResponseAsync(new BadRequestResponse("Expected an application request document."), cancellationToken);
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
}
