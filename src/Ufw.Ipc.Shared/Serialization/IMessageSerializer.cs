using Ufw.Roslyn.Controllers;

namespace Ufw.Ipc.Shared.Serialization;

public interface IMessageSerializer
{
    ValueTask<IRequestMessage> SerializeRequestAsync<T>(string route, string method, T payload, CancellationToken cancellationToken);

    ValueTask<IRequestMessage> SerializeRequestAsync(string route, string method, object? payload, Type type, CancellationToken cancellationToken);

    ValueTask<IResponseMessage> SerializeResponseAsync<T>(T payload, CancellationToken cancellationToken) where T : IIdentifiable;

    /// <summary>
    /// Encodes an already-constructed application message to JSON. Does not frame ITP.
    /// </summary>
    byte[] Encode(IMessage message);

    /// <summary>
    /// Decodes application JSON. The buffer must already have been framed and
    /// validated by ITP; garbled packets must not reach this method.
    /// </summary>
    IMessage Decode(ReadOnlyMemory<byte> buffer);
}
