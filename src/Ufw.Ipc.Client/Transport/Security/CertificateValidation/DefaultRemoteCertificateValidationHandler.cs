using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Ufw.Ipc.Client.Transport.Security.CertificateValidation;

public sealed class DefaultRemoteCertificateValidationHandler : IRemoteCertificateValidationHandler
{
    public bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
        sslPolicyErrors is SslPolicyErrors.None;
}
