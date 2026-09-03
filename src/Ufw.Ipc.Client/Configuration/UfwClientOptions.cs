using System.Security.Authentication;

namespace Ufw.Ipc.Client.Configuration;

internal sealed record UfwClientOptions(
    string ServerName,
    string PipeName,
    bool TlsEnabled,
    string? TlsServerName,
    SslProtocols SslProtocols,
    TimeSpan IoTimeout,
    TimeSpan RequestTimeout,
    string? ClientCertificatePath = null,
    string? ClientCertificateKeyPath = null);
