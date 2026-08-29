using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Roslyn.Json;

namespace Ufw.Ipc.Shared.Serialization;

internal class BufferedJsonMessageBlob : IMessageBlob
{
    private readonly byte[] _utf8;
    private readonly AotJsonSerializerContext _context;
    private bool _disposedValue;

    private BufferedJsonMessageBlob(byte[] utf8, AotJsonSerializerContext context)
    {
        _utf8 = utf8;
        _context = context;
    }

    public bool IsEmpty => _utf8.Length == 0;

    public ReadOnlyMemory<byte> Utf8 => _utf8;

    public static BufferedJsonMessageBlob CreateFrom<T>(T value, AotJsonSerializerContext serializerContext)
    {
        if (value is null or IEmptyPayload)
        {
            return EmptyBufferedJsonMessageBlob.Instance;
        }

        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(value, serializerContext.GetTypeInfo<T>());
        return new BufferedJsonMessageBlob(utf8, serializerContext);
    }

    public static BufferedJsonMessageBlob CreateFrom(object? value, Type type, AotJsonSerializerContext serializerContext)
    {
        if (value is null or IEmptyPayload)
        {
            return EmptyBufferedJsonMessageBlob.Instance;
        }

        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(value, type, serializerContext);
        return new BufferedJsonMessageBlob(utf8, serializerContext);
    }

    public static BufferedJsonMessageBlob FromUtf8(ReadOnlyMemory<byte> utf8, AotJsonSerializerContext serializerContext)
    {
        if (utf8.IsEmpty)
        {
            return EmptyBufferedJsonMessageBlob.Instance;
        }

        return new BufferedJsonMessageBlob(utf8.ToArray(), serializerContext);
    }

    public virtual ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        if (_utf8.Length == 0)
        {
            return ValueTask.FromResult<TResult?>(default);
        }

        try
        {
            TResult? result = JsonSerializer.Deserialize(_utf8, _context.GetTypeInfo<TResult>());
            return ValueTask.FromResult(result);
        }
        catch (JsonException ex)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadDeserializeFailed,
                $"Application payload is not a valid {typeof(TResult).Name}.",
                ex);
        }
    }

    public virtual void Dispose() => _disposedValue = true;

    public virtual ValueTask DisposeAsync()
    {
        _disposedValue = true;
        return ValueTask.CompletedTask;
    }

    [SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose", Justification = "Singleton empty blob owns no resources.")]
    private sealed class EmptyBufferedJsonMessageBlob() : BufferedJsonMessageBlob([], context: null!)
    {
        public static EmptyBufferedJsonMessageBlob Instance { get; } = new();

        public override ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken) where TResult : default =>
            ValueTask.FromResult<TResult?>(default);

        public override void Dispose()
        {
        }

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
