using Ufw.Roslyn.Controllers;

namespace Ufw.Ipc.Shared.Serialization;

public interface IMessageSerializer
{
    ValueTask<IMessage> SerializeAsync<T>(string id, string? method, T payload, CancellationToken cancellationToken);

    ValueTask<IMessage> SerializeAsync(string id, string? method, object? payload, Type type, CancellationToken cancellationToken);

    ValueTask<IMessage> SerializeAsync<T>(T payload, CancellationToken cancellationToken) where T : IIdentifiable;

    /// <summary>
    /// Encodes an already-constructed application message to JSON. Does not frame ITP.
    /// </summary>
    byte[] Encode(IMessage message);

    /// <summary>
    /// Decodes application JSON. The buffer must already have been framed and
    /// integrity-checked by ITP; garbled packets must not reach this method.
    /// </summary>
    IMessage Decode(ReadOnlyMemory<byte> buffer);
}
