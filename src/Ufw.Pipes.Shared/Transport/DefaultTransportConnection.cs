namespace Ufw.Pipes.Shared.Transport;

public sealed class DefaultTransportConnection(Stream innerStream) : ITransportLayerConnection, IDisposable, IAsyncDisposable
{
    private bool _disposedValue;

    public Stream GetStream(TimeSpan readTimeout, TimeSpan writeTimeout)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        return new TimedStream(innerStream, readTimeout, writeTimeout);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposedValue)
        {
            return;
        }

        await innerStream.DisposeAsync();
        _disposedValue = true;
    }

    public void Dispose()
    {
        if (_disposedValue)
        {
            return;
        }
        innerStream.Dispose();
        _disposedValue = true;
    }
}
