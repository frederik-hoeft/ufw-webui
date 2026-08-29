using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Client;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport;
using Ufw.Ipc.Shared.Transport.Itp;
using Ufw.Ipc.Shared.Transport.Security;
using Ufw.Ipc.Tests.Adapter.Transport;
using Ufw.Systemd.Api.Middleware;

namespace Ufw.Ipc.Tests.Adapter;

internal sealed class IpcTestContext
(
    IUfwClient client,
    IServiceProvider serverServices,
    IServiceProvider clientServices,
    IMessageSerializer messageSerializer,
    InProcessTransportBroker broker,
    IRequestResponsePipeline pipeline,
    ITransportSecurityService transportSecurityService,
    ItpOptions itpOptions
) : IIpcTestContext
{
    public IUfwClient Client { get; } = client;

    public IServiceProvider ServerServices { get; } = serverServices;

    public IServiceProvider ClientServices { get; } = clientServices;

    public IMessageSerializer MessageSerializer { get; } = messageSerializer;

    public async ValueTask<Stream> ConnectRawAsync(CancellationToken cancellationToken = default)
    {
        // Ownership of the connection is transferred into the returned stream via the duplex pair.
        // DefaultTransportConnection disposes the stream; callers that only need the stream should
        // connect through the broker and wrap carefully. We return the secured stream and keep the
        // connection alive by attaching it to the stream's lifetime via ownership wrapper.
        ITransportLayerConnection connection = await broker.ConnectAsync(cancellationToken).ConfigureAwait(false);
        Stream networkStream = connection.GetStream(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Stream secureStream = await transportSecurityService.OpenSecureStreamAsync(networkStream, cancellationToken).ConfigureAwait(false);
        return new TransportOwnedStream(secureStream, connection);
    }

    public async ValueTask<IMessage> ExchangeRawAsync(IMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using Stream stream = await ConnectRawAsync(cancellationToken).ConfigureAwait(false);
        ItpConnection itp = new(stream, itpOptions);
        await itp.WriteApplicationDataAsync(MessageSerializer.Encode(request), cancellationToken).ConfigureAwait(false);
        ItpFrame responseFrame = await itp.ReadAsync(cancellationToken).ConfigureAwait(false);
        return MessageSerializer.Decode(responseFrame.Payload);
    }

    public async ValueTask<IMessage> ExchangeBytesAsync(ReadOnlyMemory<byte> requestBytes, CancellationToken cancellationToken = default)
    {
        await using Stream stream = await ConnectRawAsync(cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        ItpConnection itp = new(stream, itpOptions);
        ItpFrame responseFrame = await itp.ReadAsync(cancellationToken).ConfigureAwait(false);
        return MessageSerializer.Decode(responseFrame.Payload);
    }

    public ValueTask<IMessage> ProcessPipelineAsync(IMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return pipeline.ProcessMessageAsync(request, cancellationToken);
    }

    public async ValueTask<TResponse> SendAsync<TResponse>(RequestMethod method, string route, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        TResponse response = await Client.SendAsync<TResponse>(method, route, cancellationToken).ConfigureAwait(false);
        return response;
    }

    public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(RequestMethod method, string route, TRequest request, CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse>
    {
        TResponse response = await Client.SendAsync<TRequest, TResponse>(method, route, request, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Ensures the underlying transport connection is disposed when the caller disposes the stream.
    /// </summary>
    private sealed class TransportOwnedStream(Stream inner, ITransportLayerConnection connection) : Stream
    {
        private bool _disposed;

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                inner.Dispose();
                connection.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await inner.DisposeAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
            _disposed = true;
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
