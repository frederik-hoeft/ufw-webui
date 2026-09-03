using System.Security.Authentication;

namespace Ufw.Systemd.Configuration.Model;

internal sealed class PipeOptions : IRequireValidation
{
    public string PipeName { get; init; } = "/run/ufw-systemd.pipe";

    public bool TlsEnabled { get; set; }

    public SslProtocols SslProtocols { get; set; } = SslProtocols.None;

    public RemoteCertificateValidationOptions? RemoteCertificateValidation { get; set; }

    public string? ServerCertificatePath { get; set; }

    public string? ServerCertificateKeyPath { get; set; }

    public bool AssertIsValid()
    {
        if (string.IsNullOrWhiteSpace(PipeName))
        {
            throw new InvalidOperationException("invalid pipe configuration");
        }

        if (!TlsEnabled)
        {
            if (RemoteCertificateValidation is not null)
            {
                throw new InvalidOperationException("client-certificate validation requires TLS to be enabled");
            }

            return true;
        }

        if (string.IsNullOrWhiteSpace(ServerCertificatePath)
            || string.IsNullOrWhiteSpace(ServerCertificateKeyPath)
            || !File.Exists(ServerCertificatePath)
            || !File.Exists(ServerCertificateKeyPath))
        {
            throw new InvalidOperationException("TLS requires readable server certificate and private-key files");
        }

        _ = RemoteCertificateValidation?.AssertIsValid();
        return true;
    }
}
