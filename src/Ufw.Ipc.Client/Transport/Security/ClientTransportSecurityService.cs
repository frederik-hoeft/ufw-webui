using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Transport.Security.CertificateValidation;
using Ufw.Ipc.Shared.Transport.Security;

namespace Ufw.Ipc.Client.Transport.Security;

internal sealed class ClientTransportSecurityService(IRemoteCertificateValidationHandler certificateValidationHandler, UfwClientOptions options)
    : ITransportSecurityService, IDisposable
{
    private X509Certificate2? _clientCertificate;
    private bool _disposed;

    public async Task<Stream> OpenSecureStreamAsync(Stream innerStream, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (options.SslProtocols == SslProtocols.None)
        {
            return innerStream;
        }

        SslClientAuthenticationOptions sslOptions = new()
        {
            TargetHost = options.ServerName,
            EnabledSslProtocols = options.SslProtocols,
        };
        X509Certificate2? certificate = GetOrLoadClientCertificate();
        if (certificate is not null)
        {
            sslOptions.ClientCertificates = [certificate];
        }

        SslStream stream = new(innerStream, leaveInnerStreamOpen: true, new RemoteCertificateValidationCallback(certificateValidationHandler.ValidateCertificate));
        await stream.AuthenticateAsClientAsync(sslOptions, cancellationToken);
        return stream;
    }

    private X509Certificate2? GetOrLoadClientCertificate()
    {
        if (_clientCertificate is not null)
        {
            return _clientCertificate;
        }

        if (string.IsNullOrWhiteSpace(options.ClientCertificatePath)
            || string.IsNullOrWhiteSpace(options.ClientCertificateKeyPath))
        {
            return null;
        }

        string certificatePem = File.ReadAllText(options.ClientCertificatePath);
        string keyPem = File.ReadAllText(options.ClientCertificateKeyPath);
        X509Certificate2 loaded = X509Certificate2.CreateFromPem(certificatePem, keyPem);
        _clientCertificate = loaded;
        return loaded;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _clientCertificate?.Dispose();
    }
}
