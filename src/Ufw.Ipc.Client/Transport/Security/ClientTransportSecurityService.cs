using System.Net.Security;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Transport.Security.CertificateValidation;
using Ufw.Ipc.Shared.Transport.Security;

namespace Ufw.Ipc.Client.Transport.Security;

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
        await stream.AuthenticateAsClientAsync(_sslOptions, cancellationToken);
        return stream;
    }
}
