using System.Collections.Immutable;
using Ufw.Pipes.Client.Handlers;
using Ufw.Pipes.Client.Transport;
using Ufw.Pipes.Shared;
using Ufw.Pipes.Shared.Model;
using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Pipelines;
using Ufw.Pipes.Shared.Serialization;
using Ufw.Pipes.Shared.Transport;
using Ufw.Pipes.Shared.Transport.Security;

namespace Ufw.Pipes.Client;

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

        await using ITransportLayerConnection connection = await transportLayerService.ConnectAsync(cancellationToken).NoCapture();
        await using Stream stream = connection.GetStream(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        await using Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(stream, cancellationToken).NoCapture();
        await using IMessage message = await messageSerializer.SerializeAsync(route, method, request, cancellationToken).NoCapture();

        await messageSerializer.WriteAsync(secureStream, message, cancellationToken).NoCapture();
        await secureStream.FlushAsync(cancellationToken).NoCapture();

        await using IMessage response = await messageSerializer.ReadAsync(secureStream, cancellationToken).NoCapture();

        foreach (IResponseMessageHandler handler in _handlerPipeline)
        {
            if (handler.CanHandle(response))
            {
                return await handler.TryHandleAsync<TResponse>(response, cancellationToken).NoCapture();
            }
        }

        throw new InvalidDataException($"Unable to handle unknown message type with ID '{response.Id}'. No handler has been configured for this kind of message.");
    }
}
