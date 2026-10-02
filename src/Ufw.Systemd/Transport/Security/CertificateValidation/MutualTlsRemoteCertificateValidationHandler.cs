using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Ufw.Systemd.Transport.Security.CertificateValidation;

internal sealed class MutualTlsRemoteCertificateValidationHandler(ServerTransportSecurityOptionsSnapshot options) : IRemoteCertificateValidationHandler
{
    public bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors is not SslPolicyErrors.None)
        {
            return false;
        }

        if (options.RemoteCertificateValidation is { } remoteValidation && certificate is { Issuer: { } issuer, Subject: { } subject })
        {
            return remoteValidation.RequiredSubject.Equals(subject, StringComparison.OrdinalIgnoreCase)
                && remoteValidation.RequiredIssuer.Equals(issuer, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}
