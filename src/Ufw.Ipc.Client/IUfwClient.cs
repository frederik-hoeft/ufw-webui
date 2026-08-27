using Ufw.Ipc.Shared.Model;

namespace Ufw.Ipc.Client;

public interface IUfwClient
{
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload where TResponse : IEquatable<TResponse>;

    Task<TResponse> SendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>;

    Task<TResponse> SendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>;

    Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IMessagePayload;

    Task SendAsync<TRequest>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload;

    Task SendAsync(RequestMethod method, string route, CancellationToken cancellationToken = default);
}
