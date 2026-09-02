using System.Security.Authentication;

namespace Ufw.Web.Configuration;

internal sealed class IpcClientOptions
{
    public const string SECTION_NAME = "IpcOptions";

    public string Endpoint { get; set; } = "/run/ufw-systemd.pipe";

    public SslProtocols SslProtocols { get; set; } = SslProtocols.None;

    public string? ClientCertificatePath { get; set; }

    public string? ClientCertificateKeyPath { get; set; }
}
