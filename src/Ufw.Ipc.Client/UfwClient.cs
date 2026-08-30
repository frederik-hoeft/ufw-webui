using System.Collections.Immutable;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Handlers;
using Ufw.Ipc.Client.Transport;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport;
using Ufw.Ipc.Shared.Transport.Itp;
using Ufw.Ipc.Shared.Transport.Security;

namespace Ufw.Ipc.Client;

internal sealed class UfwClient
(
    IMessageSerializer messageSerializer,
    ITransportLayerService transportLayerService,
    ITransportSecurityService transportSecurityService,
    IEnumerable<IResponseMessageHandler> handlers,
    UfwClientOptions options,
    ItpOptions itpOptions
) : IUfwClient
{
    private readonly ImmutableArray<IResponseMessageHandler> _handlerPipeline = handlers.CreatePipeline();

    public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload where TResponse : IEquatable<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<TRequest, TResponse>(request.Method ?? string.Empty, request.Id, request, cancellationToken);
    }

    public Task<TResponse> SendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        if (!RequestMethod.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "The specified request method is not supported.");
        }

        return SendRequestAsync<TResponse>(method.ToString(), route, cancellationToken).AsTask();
    }

    public Task<TResponse> SendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        if (!RequestMethod.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "The specified request method is not supported.");
        }

        return SendAsync<TRequest, TResponse>(method.ToString(), route, request, cancellationToken);
    }

    public Task<TResponse> SendAsync<TRequest, TResponse>(string method, string route, TRequest request, CancellationToken cancellationToken = default) where TResponse : IEquatable<TResponse> =>
        SendRequestAsync<TRequest, TResponse>(method, route, request, cancellationToken).AsTask();

    public Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IMessagePayload =>
        SendAsync<TRequest, OkResponse>(request, cancellationToken);

    public Task SendAsync<TRequest>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default) where TRequest : IMessagePayload =>
        SendAsync<TRequest, OkResponse>(method, route, request, cancellationToken);

    public Task SendAsync(RequestMethod method, string route, CancellationToken cancellationToken = default)
    {
        if (!RequestMethod.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "The specified request method is not supported.");
        }

        return SendRequestAsync<OkResponse>(method.ToString(), route, cancellationToken).AsTask();
    }

    private async ValueTask<TResponse> SendRequestAsync<TResponse>(string? method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        ArgumentException.ThrowIfNullOrEmpty(method, nameof(method));
        await using IRequestMessage message = await messageSerializer.SerializeRequestAsync(route, method, cancellationToken);
        return await SendMessageAsync<TResponse>(message, cancellationToken);
    }

    private async ValueTask<TResponse> SendRequestAsync<TRequest, TResponse>(string? method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        ArgumentException.ThrowIfNullOrEmpty(method, nameof(method));
        await using IRequestMessage message = await messageSerializer.SerializeRequestAsync(route, method, request, cancellationToken);
        return await SendMessageAsync<TResponse>(message, cancellationToken);
    }

    private async ValueTask<TResponse> SendMessageAsync<TResponse>(IRequestMessage message, CancellationToken cancellationToken)
        where TResponse : IEquatable<TResponse>
    {
        using CancellationTokenSource? requestTimeoutSource = CreateRequestTimeoutSource(options.RequestTimeout, cancellationToken);
        CancellationToken requestToken = requestTimeoutSource?.Token ?? cancellationToken;

        try
        {
            await using ITransportLayerConnection connection = await transportLayerService.ConnectAsync(requestToken);
            await using Stream stream = connection.GetStream(options.IoTimeout, options.IoTimeout);
            await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(stream, requestToken);

            ItpConnection itp = new(secureStream, itpOptions);
            await itp.WriteApplicationDataAsync(messageSerializer.Encode(message), requestToken);

            ItpFrame frame = await itp.ReadAsync(requestToken);
            await using IMessage decoded = messageSerializer.Decode(frame.Payload);
            if (decoded is not IResponseMessage response)
            {
                throw new ApplicationProtocolException(
                    ApplicationProtocolError.InvalidKind,
                    "Peer returned an application document that is not a response.");
            }

            foreach (IResponseMessageHandler handler in _handlerPipeline)
            {
                if (handler.CanHandle(response))
                {
                    return await handler.TryHandleAsync<TResponse>(response, requestToken);
                }
            }

            throw new InvalidDataException($"Unable to handle response status '{response.StatusCode}' with payloadType '{response.PayloadType}'. No handler has been configured for this response.");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && requestTimeoutSource?.IsCancellationRequested == true)
        {
            throw new TimeoutException("The IPC request exceeded the configured request timeout.", ex);
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
