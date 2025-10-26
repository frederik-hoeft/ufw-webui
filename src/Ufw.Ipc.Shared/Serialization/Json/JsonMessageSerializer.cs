using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ufw.Ipc.Shared.Model;
using Ufw.Roslyn.Controllers;

namespace Ufw.Ipc.Shared.Serialization.Json;

public sealed class JsonMessageSerializer(MessageJsonSerializerContext context) : IMessageSerializer
{
    private static readonly ReadOnlyMemory<byte> s_newLineBuffer = "\n"u8.ToArray();

    public async Task<IMessage> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using JsonPipeMessageBlob headerBlob = await JsonPipeMessageBlob.CreateFromAsync(stream, context, lazy: false, cancellationToken);
        MessageHeader header = await headerBlob.ReadAsync<MessageHeader>(cancellationToken);
        if (header == default)
        {
            throw new InvalidDataException("Malformed pipe message header!");
        }
        JsonPipeMessageBlob payload = await JsonPipeMessageBlob.CreateFromAsync(stream, context, cancellationToken: cancellationToken);
        return new Message(header.Context, header.Method, payload);
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    public ValueTask<IMessage> SerializeAsync<T>(string id, string? method, T payload, CancellationToken cancellationToken)
    {
        JsonPipeMessageBlob payloadBlob = JsonPipeMessageBlob.CreateFrom(payload, context);
        IMessage message = new Message(id, method, payloadBlob);
        return ValueTask.FromResult(message);
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    public ValueTask<IMessage> SerializeAsync(string id, string? method, object? payload, Type type, CancellationToken cancellationToken)
    {
        JsonPipeMessageBlob from = JsonPipeMessageBlob.CreateFrom(payload, type, context);
        IMessage message = new Message(id, method, from);
        return ValueTask.FromResult(message);
    }

    public ValueTask<IMessage> SerializeAsync<T>(T payload, CancellationToken cancellationToken) where T : IIdentifiable => 
        SerializeAsync(payload.Id, payload.Method, payload, cancellationToken);

    public async Task WriteAsync(Stream stream, IMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);
        MessageHeader header = new(message.Method, message.Id);
        await JsonSerializer.SerializeAsync(stream, header, context.GetTypeInfo<MessageHeader>(), cancellationToken);
        await stream.WriteAsync(s_newLineBuffer, cancellationToken);
        await using Stream payloadStream = await message.Payload.CreateStreamAsync(cancellationToken);
        await payloadStream.CopyToAsync(stream, cancellationToken);
        await stream.WriteAsync(s_newLineBuffer, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}