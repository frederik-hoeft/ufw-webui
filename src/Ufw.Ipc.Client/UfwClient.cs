using System.Collections.Immutable;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Handlers;
using Ufw.Ipc.Client.Transport;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Pipelines;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Ipc.Client;

internal sealed class UfwClient
(
    IMessageSerializer messageSerializer,
    IClientMessageExchange messageExchange,
    IEnumerable<IResponseMessageHandler> handlers,
    UfwClientOptions options
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
            await using IResponseMessage response = await messageExchange.ExchangeAsync(message, requestToken);
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
