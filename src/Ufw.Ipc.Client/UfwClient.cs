using System.Collections.Immutable;
using Ufw.Ipc.Client.Handlers;
using Ufw.Ipc.Client.Transport;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport;
using Ufw.Ipc.Shared.Transport.Security;

namespace Ufw.Ipc.Client;

internal sealed class UfwClient
(
    IMessageSerializer messageSerializer,
    ITransportLayerService transportLayerService,
    ITransportSecurityService transportSecurityService,
    IEnumerable<IResponseMessageHandler> handlers
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

        return SendAsync<object?, TResponse>(method.ToString(), route, request: null, cancellationToken);
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

    public Task SendAsync(RequestMethod method, string route, CancellationToken cancellationToken = default) =>
        SendAsync<object?, OkResponse>(method, route, request: null, cancellationToken);

    private async ValueTask<TResponse> SendRequestAsync<TRequest, TResponse>(string? method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        ArgumentException.ThrowIfNullOrEmpty(method, nameof(method));

        await using ITransportLayerConnection connection = await transportLayerService.ConnectAsync(cancellationToken);
        await using Stream stream = connection.GetStream(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(stream, cancellationToken);
        await using IMessage message = await messageSerializer.SerializeAsync(route, method, request, cancellationToken);

        await messageSerializer.WriteAsync(secureStream, message, cancellationToken);
        await secureStream.FlushAsync(cancellationToken);

        await using IMessage response = await messageSerializer.ReadAsync(secureStream, cancellationToken);

        foreach (IResponseMessageHandler handler in _handlerPipeline)
        {
            if (handler.CanHandle(response))
            {
                return await handler.TryHandleAsync<TResponse>(response, cancellationToken);
            }
        }

        throw new InvalidDataException($"Unable to handle unknown message type with ID '{response.Id}'. No handler has been configured for this kind of message.");
    }
}
