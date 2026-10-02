using System.Net;

namespace Ufw.Systemd.Configuration.Model;

internal sealed class TcpOptions : IRequireValidation
{
    public required string ListenAddress { get; init; }

    public required int Port { get; init; }

    public void ThrowIfInvalid()
    {
        if (!IPAddress.TryParse(ListenAddress, out _) || Port is < 1 or > IPEndPoint.MaxPort)
        {
            throw new InvalidOperationException("A TCP endpoint requires a literal IP listen address and a port between 1 and 65535.");
        }
    }
}
