using System.Security.Authentication;

namespace Ufw.Web.Configuration;

internal sealed class IpcClientOptions
{
    public const string SECTION_NAME = "IpcOptions";

    public string Endpoint { get; set; } = "/run/ufw-systemd.pipe";

    public bool TlsEnabled { get; set; }

    public string? TlsServerName { get; set; }

    public SslProtocols SslProtocols { get; set; } = SslProtocols.None;

    public string? ClientCertificatePath { get; set; }

    public string? ClientCertificateKeyPath { get; set; }

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            return false;
        }

        if (TlsEnabled && string.IsNullOrWhiteSpace(TlsServerName))
        {
            return false;
        }

        bool hasCertificate = !string.IsNullOrWhiteSpace(ClientCertificatePath);
        bool hasKey = !string.IsNullOrWhiteSpace(ClientCertificateKeyPath);
        if (hasCertificate != hasKey)
        {
            return false;
        }

        return TlsEnabled || !hasCertificate;
    }
}
