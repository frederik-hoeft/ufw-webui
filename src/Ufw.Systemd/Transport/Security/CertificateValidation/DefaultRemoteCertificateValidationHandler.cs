using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Ufw.Systemd.Transport.Security.CertificateValidation;

internal sealed class DefaultRemoteCertificateValidationHandler : IRemoteCertificateValidationHandler
{
    public bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
        sslPolicyErrors is SslPolicyErrors.None;
}
