using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Transport.Security.CertificateValidation;
using Ufw.Ipc.Shared.Security.Certificates;
using Ufw.Ipc.Shared.Threading;
using Ufw.Ipc.Shared.Transport.Security;

namespace Ufw.Ipc.Client.Transport.Security;

internal sealed class ClientTransportSecurityService
(
    IRemoteCertificateValidationHandler certificateValidationHandler,
    ICertificateLoader certificateLoader,
    UfwClientOptions options
) : ITransportSecurityService, IDisposable
{
    private readonly AsyncLock _certificateLock = new();
    private X509Certificate2? _clientCertificate;
    private bool _disposed;

    public async Task<Stream> OpenSecureStreamAsync(Stream innerStream, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!options.TlsEnabled)
        {
            return innerStream;
        }

        SslClientAuthenticationOptions sslOptions = new()
        {
            TargetHost = options.TlsServerName ?? throw new InvalidOperationException("TLS server name is not configured."),
            // SslProtocols.None intentionally preserves the .NET/OS automatic-selection semantics.
            EnabledSslProtocols = options.SslProtocols,
        };
        X509Certificate2? certificate = await GetOrLoadClientCertificateAsync(cancellationToken);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (certificate is not null)
        {
            sslOptions.ClientCertificates = [certificate];
        }

        SslStream stream = new(innerStream, leaveInnerStreamOpen: true, new RemoteCertificateValidationCallback(certificateValidationHandler.ValidateCertificate));
        await stream.AuthenticateAsClientAsync(sslOptions, cancellationToken);
        return stream;
    }

    private async ValueTask<X509Certificate2?> GetOrLoadClientCertificateAsync(CancellationToken cancellationToken)
    {
        if (options.ClientCertificatePath is null || options.ClientCertificateKeyPath is null)
        {
            return null;
        }

        X509Certificate2? certificate = Volatile.Read(ref _clientCertificate);
        if (certificate is not null)
        {
            return certificate;
        }

        return await _certificateLock.RunTaskAsync(async ct =>
        {
            certificate = Volatile.Read(ref _clientCertificate);
            if (certificate is not null)
            {
                return certificate;
            }

            X509Certificate2 loaded = await certificateLoader.LoadCertificateAsync(
                options.ClientCertificatePath,
                options.ClientCertificateKeyPath,
                ct);
            Volatile.Write(ref _clientCertificate, loaded);
            return loaded;
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _certificateLock.Dispose();
        _clientCertificate?.Dispose();
    }
}
