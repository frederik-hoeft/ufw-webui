using System.Net.Security;
using Ufw.Pipes.Client.Configuration;
using Ufw.Pipes.Client.Transport.Security.CertificateValidation;
using Ufw.Pipes.Shared;
using Ufw.Pipes.Shared.Transport.Security;

namespace Ufw.Pipes.Client.Transport.Security;

internal sealed class ClientTransportSecurityService(IRemoteCertificateValidationHandler certificateValidationHandler, UfwClientOptions options) : ITransportSecurityService
{
    private readonly SslClientAuthenticationOptions _sslOptions = new()
    {
        TargetHost = options.ServerName,
        EnabledSslProtocols = options.SslProtocols
    };

    public async Task<Stream> OpenSecureStreamAsync(Stream innerStream, CancellationToken cancellationToken = default)
    {
        SslStream stream = new(innerStream, leaveInnerStreamOpen: true, new RemoteCertificateValidationCallback(certificateValidationHandler.ValidateCertificate));
        await stream.AuthenticateAsClientAsync(_sslOptions, cancellationToken).NoCapture();
        return stream;
    }
}
