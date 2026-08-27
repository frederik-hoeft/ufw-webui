using Ufw.Roslyn.Controllers;

namespace Ufw.Ipc.Shared.Serialization;

public interface IMessageSerializer
{
    ValueTask<IMessage> SerializeAsync<T>(string id, string? method, T payload, CancellationToken cancellationToken);

    ValueTask<IMessage> SerializeAsync(string id, string? method, object? payload, Type type, CancellationToken cancellationToken);

    ValueTask<IMessage> SerializeAsync<T>(T payload, CancellationToken cancellationToken) where T : IIdentifiable;

    Task<IMessage> ReadAsync(Stream stream, CancellationToken cancellationToken);

    Task WriteAsync(Stream stream, IMessage message, CancellationToken cancellationToken);
}
