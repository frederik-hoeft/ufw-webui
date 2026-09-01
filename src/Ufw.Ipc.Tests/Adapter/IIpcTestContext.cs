using Ufw.Ipc.Client;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Tests.Adapter;

/// <summary>
/// Per-run facade exposed to test lambdas. Owns no lifetime beyond the enclosing <c>RunAsync</c> call.
/// </summary>
public interface IIpcTestContext
{
    /// <summary>
    /// Typed production client bound to the in-process transport.
    /// </summary>
    IUfwClient Client { get; }

    /// <summary>
    /// Root server DI container for the current run (not a scope).
    /// </summary>
    IServiceProvider ServerServices { get; }

    /// <summary>
    /// Root client DI container for the current run (not a scope).
    /// </summary>
    IServiceProvider ClientServices { get; }

    /// <summary>
    /// Application serializer used by the server host. ITP framing is composed separately
    /// by the raw exchange helpers.
    /// </summary>
    IMessageSerializer MessageSerializer { get; }

    /// <summary>
    /// Opens a raw duplex stream to a server worker, bypassing <see cref="IUfwClient"/>.
    /// The caller owns the returned stream.
    /// </summary>
    ValueTask<Stream> ConnectRawAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a fully formed request envelope and returns the response envelope.
    /// </summary>
    ValueTask<IResponseMessage> ExchangeRawAsync(IRequestMessage request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends arbitrary application bytes inside a valid ITP ApplicationData frame.
    /// Useful for malformed or directionally invalid application envelopes that cannot be represented by runtime message types.
    /// </summary>
    ValueTask<IResponseMessage> ExchangeApplicationBytesAsync(ReadOnlyMemory<byte> applicationBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes arbitrary request bytes (including malformed frames) and attempts to read one response envelope.
    /// </summary>
    ValueTask<IResponseMessage> ExchangeBytesAsync(ReadOnlyMemory<byte> requestBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes the server request/response pipeline without touching the transport.
    /// Useful for routing-only tests.
    /// </summary>
    ValueTask<IResponseMessage> ProcessPipelineAsync(IRequestMessage request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience typed send via <see cref="Client"/>.
    /// </summary>
    ValueTask<TResponse> SendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>;

    /// <summary>
    /// Convenience typed send via <see cref="Client"/>.
    /// </summary>
    ValueTask<TResponse> SendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>;
}
