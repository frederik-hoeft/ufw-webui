using System.Text.Json;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Roslyn.Json;

namespace Ufw.Ipc.Shared.Serialization;

internal sealed class BufferedJsonMessageBlob : IMessageBlob
{
    private readonly byte[] _utf8;
    private readonly AotJsonSerializerContext _context;
    private readonly bool _isJsonNull;
    private bool _disposedValue;

    private BufferedJsonMessageBlob(byte[] utf8, AotJsonSerializerContext context, bool hasPayload, bool isJsonNull)
    {
        _utf8 = utf8;
        _context = context;
        HasPayload = hasPayload;
        _isJsonNull = isJsonNull;
    }

    public bool HasPayload { get; }

    public ReadOnlyMemory<byte> Utf8
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return _utf8;
        }
    }

    public static BufferedJsonMessageBlob Empty(AotJsonSerializerContext serializerContext) =>
        new([], serializerContext, hasPayload: false, isJsonNull: false);

    public static BufferedJsonMessageBlob CreateFrom<T>(T value, AotJsonSerializerContext serializerContext)
    {
        if (value is IEmptyPayload)
        {
            return Empty(serializerContext);
        }

        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(value, serializerContext.GetTypeInfo<T>());
        return new BufferedJsonMessageBlob(utf8, serializerContext, hasPayload: true, isJsonNull: value is null);
    }

    public static BufferedJsonMessageBlob CreateFrom(object? value, Type type, AotJsonSerializerContext serializerContext)
    {
        if (value is IEmptyPayload)
        {
            return Empty(serializerContext);
        }

        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(value, type, serializerContext);
        return new BufferedJsonMessageBlob(utf8, serializerContext, hasPayload: true, isJsonNull: value is null);
    }

    public static BufferedJsonMessageBlob FromJsonElement(JsonElement payload, AotJsonSerializerContext serializerContext)
    {
        if (payload.ValueKind == JsonValueKind.Undefined)
        {
            return Empty(serializerContext);
        }

        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(payload, serializerContext.GetTypeInfo<JsonElement>());
        return new BufferedJsonMessageBlob(
            utf8,
            serializerContext,
            hasPayload: true,
            isJsonNull: payload.ValueKind == JsonValueKind.Null);
    }

    public ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (!HasPayload)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.MissingPayload,
                "Application message does not contain a payload.");
        }

        if (_isJsonNull && default(TResult) is not null)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadDeserializeFailed,
                $"Application payload JSON null cannot be bound to non-nullable value type {typeof(TResult).Name}.");
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

    public void Dispose() => _disposedValue = true;

    public ValueTask DisposeAsync()
    {
        _disposedValue = true;
        return ValueTask.CompletedTask;
    }
}
