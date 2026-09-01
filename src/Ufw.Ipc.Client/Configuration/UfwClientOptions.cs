using System.Security.Authentication;

namespace Ufw.Ipc.Client.Configuration;

internal sealed record UfwClientOptions(
    string ServerName,
    string PipeName,
    SslProtocols SslProtocols,
    TimeSpan IoTimeout,
    TimeSpan RequestTimeout)
{
    public UfwClientOptions(string ServerName, string PipeName, SslProtocols SslProtocols)
        : this(
            ServerName,
            PipeName,
            SslProtocols,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(15))
    {
    }
}
