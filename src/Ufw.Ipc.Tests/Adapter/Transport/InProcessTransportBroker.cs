using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Ufw.Ipc.Shared.Transport;

namespace Ufw.Ipc.Tests.Adapter.Transport;

/// <summary>
/// Matches in-process client connect calls with server accept calls using duplex stream pairs.
/// Each broker instance is isolated; create one per test host so parallel tests never share state.
/// </summary>
internal sealed class InProcessTransportBroker : IAsyncDisposable
{
    private readonly Channel<ITransportLayerConnection> _pendingServerConnections = Channel.CreateUnbounded<ITransportLayerConnection>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

    private bool _disposed;

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Ownership of both connections is transferred: server side to the accept queue, client side to the caller.")]
    public async ValueTask<ITransportLayerConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        (Stream clientStream, Stream serverStream) = DuplexStreamPair.Create();
        DefaultTransportConnection serverConnection = new(serverStream);
        DefaultTransportConnection clientConnection = new(clientStream);

        try
        {
            await _pendingServerConnections.Writer.WriteAsync(serverConnection, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await serverConnection.DisposeAsync().ConfigureAwait(false);
            await clientConnection.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return clientConnection;
    }

    public async ValueTask<ITransportLayerConnection> AcceptAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            return await _pendingServerConnections.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException ex)
        {
            throw new OperationCanceledException("The in-process transport broker has been disposed.", ex, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pendingServerConnections.Writer.TryComplete();

        while (_pendingServerConnections.Reader.TryRead(out ITransportLayerConnection? orphaned))
        {
            await orphaned.DisposeAsync().ConfigureAwait(false);
        }
    }
}
