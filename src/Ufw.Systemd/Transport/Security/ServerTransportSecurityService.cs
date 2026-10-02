using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Ufw.Shared.Ipc.Transport.Security;
using Ufw.Shared.Security.Certificates;
using Ufw.Shared.Threading;
using Ufw.Systemd.Transport.Security.CertificateValidation;

namespace Ufw.Systemd.Transport.Security;

internal sealed class ServerTransportSecurityService(
    IRemoteCertificateValidationHandler certificateValidationHandler,
    ServerTransportSecurityOptionsSnapshot options,
    ICertificateLoader certificateLoader) : ITransportSecurityService, IDisposable
{
    private readonly AsyncLock _lock = new();
    private SslServerAuthenticationOptions? _sslOptions;
    private bool _disposedValue;

    public async Task<Stream> OpenSecureStreamAsync(Stream innerStream, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        if (!options.TlsEnabled)
        {
            return innerStream;
        }

        SslServerAuthenticationOptions? sslOptions = Volatile.Read(in _sslOptions);
        sslOptions ??= await _lock.RunTaskAsync(CreateSslOptionsUnsynchronizedAsync, cancellationToken);

        ObjectDisposedException.ThrowIf(_disposedValue, this);
        RemoteCertificateValidationCallback? validationCallback = options.RemoteCertificateValidation is null
            ? null
            : new RemoteCertificateValidationCallback(certificateValidationHandler.ValidateCertificate);
        SslStream stream = new(innerStream, leaveInnerStreamOpen: true, validationCallback);
        await stream.AuthenticateAsServerAsync(sslOptions, cancellationToken);
        return stream;
    }

    private async Task<SslServerAuthenticationOptions> CreateSslOptionsUnsynchronizedAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_lock.IsHeld);
        SslServerAuthenticationOptions? sslOptions = Volatile.Read(in _sslOptions);
        if (sslOptions != null)
        {
            return sslOptions;
        }

        X509Certificate2 certificate = await certificateLoader.LoadCertificateAsync(options.ServerCertificatePath!, options.ServerCertificateKeyPath!, cancellationToken);
        sslOptions = new SslServerAuthenticationOptions
        {
            // SslProtocols.None intentionally preserves the .NET/OS automatic-selection semantics.
            EnabledSslProtocols = options.SslProtocols,
            ClientCertificateRequired = options.RemoteCertificateValidation is not null,
            ServerCertificate = certificate,
        };
        Volatile.Write(ref _sslOptions, sslOptions);
        return sslOptions;
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _lock.Dispose();
                _sslOptions?.ServerCertificate?.Dispose();
            }

            _sslOptions = null;
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
