using System.Net;
using System.Net.Sockets;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Transport.Tcp;

internal sealed class TcpServerStreamDescriptor : ITcpServerStreamDescriptor, IDisposable
{
    private readonly TcpListener _listener;
    private bool _disposedValue;

    public TcpServerStreamDescriptor(IConfiguration configuration)
    {
        _listener = new(IPAddress.Loopback, 1234);
        _listener.Start();
    }

    public async Task<NetworkStream> ServeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        Socket socket = await _listener.AcceptSocketAsync(cancellationToken);
        return new NetworkStream(socket, ownsSocket: true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _listener.Stop();
                _listener.Server.Dispose();
                _listener.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
