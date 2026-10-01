using System.Security.Authentication;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Transport.Security;

internal sealed class ServerTransportSecurityOptionsSnapshot
{
    public ServerTransportSecurityOptionsSnapshot(IConfiguration configuration)
    {
        TransportSecurityOptions options = configuration.Settings.Transport.Security;
        TlsEnabled = options.TlsEnabled;
        SslProtocols = options.SslProtocols;
        RemoteCertificateValidation = options.RemoteCertificateValidation;
        ServerCertificatePath = options.ServerCertificatePath;
        ServerCertificateKeyPath = options.ServerCertificateKeyPath;
    }

    public bool TlsEnabled { get; }

    public SslProtocols SslProtocols { get; }

    public RemoteCertificateValidationOptions? RemoteCertificateValidation { get; }

    public string? ServerCertificatePath { get; }

    public string? ServerCertificateKeyPath { get; }
}
