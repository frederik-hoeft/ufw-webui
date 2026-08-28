namespace Ufw.Ipc.Shared.Serialization;

public interface IMessageBlob : IDisposable, IAsyncDisposable
{
    bool IsEmpty { get; }

    ReadOnlyMemory<byte> Utf8 { get; }

    ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken);

    ValueTask<Stream> CreateStreamAsync(CancellationToken cancellationToken);

    ValueTask<bool> TryReadAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
