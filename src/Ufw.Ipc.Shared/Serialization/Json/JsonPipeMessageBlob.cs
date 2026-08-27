using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ufw.Ipc.Shared.Model;
using Ufw.Roslyn.Json;

namespace Ufw.Ipc.Shared.Serialization.Json;

internal class JsonPipeMessageBlob : IMessageBlob, IDisposable, IAsyncDisposable
{
    private readonly MemoryStream _buffer;
    private readonly AotJsonSerializerContext _context;
    private readonly Stream? _source;
    private bool _disposedValue;

    private JsonPipeMessageBlob(MemoryStream stream, AotJsonSerializerContext context, Stream? source)
    {
        _buffer = stream;
        _context = context;
        _source = source;
    }

    public static JsonPipeMessageBlob CreateFrom<T>(T value, AotJsonSerializerContext serializerContext)
    {
        if (value is null or IEmptyPayload)
        {
            return EmptyJsonPipeMessageBlob.Instance;
        }

        MemoryStream memoryStream = new();
        JsonSerializer.Serialize(memoryStream, value, serializerContext.GetTypeInfo<T>());
        return new JsonPipeMessageBlob(memoryStream, serializerContext, source: null);
    }

    public static JsonPipeMessageBlob CreateFrom(object? value, Type type, AotJsonSerializerContext serializerContext)
    {
        if (value is null or IEmptyPayload)
        {
            return EmptyJsonPipeMessageBlob.Instance;
        }

        MemoryStream memoryStream = new();
        JsonSerializer.Serialize(memoryStream, value, type, serializerContext);
        return new JsonPipeMessageBlob(memoryStream, serializerContext, null);
    }

    public static async ValueTask<JsonPipeMessageBlob> CreateFromAsync(Stream stream, AotJsonSerializerContext serializerContext, bool lazy = true, CancellationToken cancellationToken = default)
    {
        MemoryStream buffer = new();
        if (lazy)
        {
            return new JsonPipeMessageBlob(buffer, serializerContext, stream);
        }

        await ReadLineAsync(stream, buffer, cancellationToken);
        return new JsonPipeMessageBlob(buffer, serializerContext, source: null);
    }

    public virtual async ValueTask<Stream> CreateStreamAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        await BufferDataAsync(cancellationToken);
        MemoryStream result = new((int)_buffer.Length);
        if (_buffer.Length > 0)
        {
            _buffer.Seek(0, SeekOrigin.Begin);
            await _buffer.CopyToAsync(result, cancellationToken);
            result.Seek(0, SeekOrigin.Begin);
        }
        return result;
    }

    public virtual async ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        await BufferDataAsync(cancellationToken);
        if (_buffer.Length == 0)
        {
            return default;
        }
        _buffer.Seek(0, SeekOrigin.Begin);
        try
        {
            return await JsonSerializer.DeserializeAsync(_buffer, _context.GetTypeInfo<TResult>(), cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public virtual async ValueTask<bool> TryReadAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await BufferDataAsync(cancellationToken);
            return true;
        }
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        using CancellationTokenSource timeoutCts = new(timeout);
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await BufferDataAsync(cts.Token);
        }
        catch (TaskCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            throw;
        }
        return true;
    }

    private async ValueTask BufferDataAsync(CancellationToken cancellationToken)
    {
        if (_source == null || _buffer.Length != 0)
        {
            return;
        }
        await ReadLineAsync(_source, _buffer, cancellationToken);
    }

    private static async ValueTask ReadLineAsync(Stream source, MemoryStream destination, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(1);
        Memory<byte> memory = buffer.AsMemory()[..1];
        while (true)
        {
            int bytesRead = await source.ReadAtLeastAsync(memory, minimumBytes: 1, throwOnEndOfStream: false, cancellationToken);
            byte byteRead = buffer[0];
            if (bytesRead != 1 || byteRead == '\n')
            {
                break;
            }
            destination.WriteByte(byteRead);
        }
        ArrayPool<byte>.Shared.Return(buffer, false);
    }

    public virtual void Dispose()
    {
        if (_disposedValue)
        {
            return;
        }
        _buffer.Dispose();
        _disposedValue = true;
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_disposedValue)
        {
            return;
        }

        await _buffer.DisposeAsync();
        _disposedValue = true;
    }

    [SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose", Justification = "Dummy implementation, no resources allocated")]
    private sealed class EmptyJsonPipeMessageBlob() : JsonPipeMessageBlob(stream: null!, context: null!, source: null)
    {
        public static EmptyJsonPipeMessageBlob Instance { get; } = new EmptyJsonPipeMessageBlob();

        public override ValueTask<Stream> CreateStreamAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Stream.Null);

        public override ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken) where TResult : default =>
            ValueTask.FromResult<TResult?>(default);

        // Empty payloads have nothing to buffer; draining them is a no-op success so middleware can safely consume the body.
        public override ValueTask<bool> TryReadAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);

        public override void Dispose()
        {
        }

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
