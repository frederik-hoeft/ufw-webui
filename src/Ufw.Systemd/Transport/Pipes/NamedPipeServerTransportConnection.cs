using System.IO.Pipes;
using Ufw.Pipes.Shared.Transport;

namespace Ufw.Systemd.Transport.Pipes;

internal sealed class NamedPipeServerTransportConnection(NamedPipeServerStream pipe) : ITransportLayerConnection, IDisposable, IAsyncDisposable
{
    private bool _disposedValue;

    public Stream GetStream(TimeSpan readTimeout, TimeSpan writeTimeout)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        return new TimedStream(pipe, readTimeout, writeTimeout);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposedValue)
        {
            return;
        }
        await pipe.DisposeAsync();
        _disposedValue = true;
    }

    public void Dispose()
    {
        if (_disposedValue)
        {
            return;
        }
        pipe.Dispose();
        _disposedValue = true;
    }
}
