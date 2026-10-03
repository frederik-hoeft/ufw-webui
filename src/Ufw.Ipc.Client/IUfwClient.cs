using Ufw.Shared.Ipc.Model;

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

    /// <summary>
    /// Sends a typed request without throwing when the daemon returns a non-success application response.
    /// </summary>
    /// <remarks>Transport, cancellation, timeout, and malformed-response failures remain exceptional.</remarks>
    Task<UfwIpcResult<TResponse>> TrySendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload where TResponse : IEquatable<TResponse>;

    /// <summary>
    /// Sends a typed request without throwing when the daemon returns a non-success application response.
    /// </summary>
    /// <remarks>Transport, cancellation, timeout, and malformed-response failures remain exceptional.</remarks>
    Task<UfwIpcResult<TResponse>> TrySendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>;

    /// <summary>
    /// Sends a request without throwing when the daemon returns a non-success application response.
    /// </summary>
    /// <remarks>Transport, cancellation, timeout, and malformed-response failures remain exceptional.</remarks>
    Task<UfwIpcResult<TResponse>> TrySendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>;

    /// <summary>
    /// Sends a typed request without throwing when the daemon returns a non-success application response.
    /// </summary>
    /// <remarks>Transport, cancellation, timeout, and malformed-response failures remain exceptional.</remarks>
    Task<UfwIpcResult> TrySendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IMessagePayload;

    /// <summary>
    /// Sends a typed request without throwing when the daemon returns a non-success application response.
    /// </summary>
    /// <remarks>Transport, cancellation, timeout, and malformed-response failures remain exceptional.</remarks>
    Task<UfwIpcResult> TrySendAsync<TRequest>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IMessagePayload;

    /// <summary>
    /// Sends a request without throwing when the daemon returns a non-success application response.
    /// </summary>
    /// <remarks>Transport, cancellation, timeout, and malformed-response failures remain exceptional.</remarks>
    Task<UfwIpcResult> TrySendAsync(RequestMethod method, string route, CancellationToken cancellationToken = default);
}
