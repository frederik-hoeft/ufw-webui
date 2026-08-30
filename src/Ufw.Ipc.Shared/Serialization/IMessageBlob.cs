namespace Ufw.Ipc.Shared.Serialization;

public interface IMessageBlob : IDisposable, IAsyncDisposable
{
    bool HasPayload { get; }

    ReadOnlyMemory<byte> Utf8 { get; }

    ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken);
}
