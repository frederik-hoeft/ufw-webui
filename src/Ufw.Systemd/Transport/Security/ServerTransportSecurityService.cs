using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Ufw.Ipc.Shared.Security.Certificates;
using Ufw.Ipc.Shared.Threading;
using Ufw.Ipc.Shared.Transport.Security;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Transport.Security.CertificateValidation;

namespace Ufw.Systemd.Transport.Security;

internal sealed class ServerTransportSecurityService
(
    IRemoteCertificateValidationHandler certificateValidationHandler,
    IConfiguration configuration,
    ICertificateLoader certificateLoader
) : ITransportSecurityService, IDisposable
{
    private readonly AsyncLock _lock = new();
    private SslServerAuthenticationOptions? _sslOptions;
    private bool _disposedValue;

    public async Task<Stream> OpenSecureStreamAsync(Stream innerStream, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        if (!configuration.Settings.Pipe.TlsEnabled)
        {
            return innerStream;
        }

        SslServerAuthenticationOptions? sslOptions = Volatile.Read(in _sslOptions);
        sslOptions ??= await _lock.RunTaskAsync(CreateSslOptionsUnsynchronizedAsync, cancellationToken);

        ObjectDisposedException.ThrowIf(_disposedValue, this);
        RemoteCertificateValidationCallback? validationCallback = configuration.Settings.Pipe.RemoteCertificateValidation is null
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

        PipeOptions pipeOptions = configuration.Settings.Pipe;
        pipeOptions.AssertIsValid();
        X509Certificate2 certificate = await certificateLoader.LoadCertificateAsync(
            pipeOptions.ServerCertificatePath!,
            pipeOptions.ServerCertificateKeyPath!,
            cancellationToken);

        sslOptions = new SslServerAuthenticationOptions
        {
            // SslProtocols.None intentionally preserves the .NET/OS automatic-selection semantics.
            EnabledSslProtocols = pipeOptions.SslProtocols,
            ClientCertificateRequired = pipeOptions.RemoteCertificateValidation is not null,
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
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
