namespace Ufw.Ipc.Shared.Serialization;

public interface IMessageBlob : IDisposable, IAsyncDisposable
{
    ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken);

    ValueTask<Stream> CreateStreamAsync(CancellationToken cancellationToken);

    ValueTask<bool> TryReadAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
