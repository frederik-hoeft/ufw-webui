using System.Net;
using System.Net.Sockets;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Transport.Tcp;

internal sealed class TcpServerStreamDescriptor : ITcpServerStreamDescriptor, IDisposable
{
    private readonly TcpListener _listener;
    private bool _disposed;

    public TcpServerStreamDescriptor(IConfiguration configuration)
    {
        TcpOptions options = configuration.Settings.Transport.Tcp ?? throw new InvalidOperationException("TCP transport settings are not configured.");
        _listener = new TcpListener(IPAddress.Parse(options.ListenAddress), options.Port);
        _listener.Start();
    }

    public async Task<NetworkStream> ServeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Socket socket = await _listener.AcceptSocketAsync(cancellationToken);
        return new NetworkStream(socket, ownsSocket: true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listener.Stop();
    }
}
