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

    public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload where TResponse : IEquatable<TResponse>
    {
        UfwIpcResult<TResponse> result = await TrySendAsync<TRequest, TResponse>(request, cancellationToken);
        return result.Result;
    }

    public async Task<TResponse> SendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        UfwIpcResult<TResponse> result = await TrySendAsync<TResponse>(method, route, cancellationToken);
        return result.Result;
    }

    public async Task<TResponse> SendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        UfwIpcResult<TResponse> result = await TrySendAsync<TRequest, TResponse>(method, route, request, cancellationToken);
        return result.Result;
    }

    public async Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IMessagePayload
    {
        UfwIpcResult result = await TrySendAsync(request, cancellationToken);
        result.EnsureSuccess();
    }

    public async Task SendAsync<TRequest>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default) where TRequest : IMessagePayload
    {
        UfwIpcResult result = await TrySendAsync(method, route, request, cancellationToken);
        result.EnsureSuccess();
    }

    public async Task SendAsync(RequestMethod method, string route, CancellationToken cancellationToken = default)
    {
        UfwIpcResult result = await TrySendAsync(method, route, cancellationToken);
        result.EnsureSuccess();
    }

    public Task<UfwIpcResult<TResponse>> TrySendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload where TResponse : IEquatable<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        return TrySendAsync<TRequest, TResponse>(request.Method ?? string.Empty, request.Id, request, cancellationToken);
    }

    public Task<UfwIpcResult<TResponse>> TrySendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        ValidateMethod(method);
        return TrySendRequestAsync<TResponse>(method.ToString(), route, cancellationToken).AsTask();
    }

    public Task<UfwIpcResult<TResponse>> TrySendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        ValidateMethod(method);
        return TrySendAsync<TRequest, TResponse>(method.ToString(), route, request, cancellationToken);
    }

    public Task<UfwIpcResult<TResponse>> TrySendAsync<TRequest, TResponse>(string method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse> =>
        TrySendRequestAsync<TRequest, TResponse>(method, route, request, cancellationToken).AsTask();

    public async Task<UfwIpcResult> TrySendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IMessagePayload
    {
        UfwIpcResult<OkResponse> result = await TrySendAsync<TRequest, OkResponse>(request, cancellationToken);
        return UfwIpcResult.FromPayloadResult(result);
    }

    public async Task<UfwIpcResult> TrySendAsync<TRequest>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default) where TRequest : IMessagePayload
    {
        UfwIpcResult<OkResponse> result = await TrySendAsync<TRequest, OkResponse>(method, route, request, cancellationToken);
        return UfwIpcResult.FromPayloadResult(result);
    }

    public async Task<UfwIpcResult> TrySendAsync(RequestMethod method, string route, CancellationToken cancellationToken = default)
    {
        UfwIpcResult<OkResponse> result = await TrySendAsync<OkResponse>(method, route, cancellationToken);
        return UfwIpcResult.FromPayloadResult(result);
    }

    private async ValueTask<UfwIpcResult<TResponse>> TrySendRequestAsync<TResponse>(string? method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        ArgumentException.ThrowIfNullOrEmpty(method, nameof(method));
        await using IRequestMessage message = await messageSerializer.SerializeRequestAsync(route, method, cancellationToken);
        return await TrySendMessageAsync<TResponse>(message, cancellationToken);
    }

    private async ValueTask<UfwIpcResult<TResponse>> TrySendRequestAsync<TRequest, TResponse>(string? method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        ArgumentException.ThrowIfNullOrEmpty(method, nameof(method));
        await using IRequestMessage message = await messageSerializer.SerializeRequestAsync(route, method, request, cancellationToken);
        return await TrySendMessageAsync<TResponse>(message, cancellationToken);
    }

    private async ValueTask<UfwIpcResult<TResponse>> TrySendMessageAsync<TResponse>(IRequestMessage message, CancellationToken cancellationToken)
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

    private static void ValidateMethod(RequestMethod method)
    {
        if (!RequestMethod.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "The specified request method is not supported.");
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
