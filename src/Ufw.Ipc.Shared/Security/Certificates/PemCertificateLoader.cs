using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Ufw.Ipc.Shared.Security.Certificates;

public sealed class PemCertificateLoader : ICertificateLoader
{
    public async ValueTask<X509Certificate2> LoadCertificateAsync(string certificatePath, string? certificateKeyPath, CancellationToken cancellationToken = default)
    {
        string certificatePem = await File.ReadAllTextAsync(certificatePath, cancellationToken);
        if (certificateKeyPath == null)
        {
            return X509Certificate2.CreateFromPem(certificatePem);
        }

        string keyPem = await File.ReadAllTextAsync(certificateKeyPath, cancellationToken);
        X509Certificate2 certificate = X509Certificate2.CreateFromPem(certificatePem, keyPem);
        if (!OperatingSystem.IsWindows())
        {
            return certificate;
        }

        // Schannel cannot reliably use the ephemeral private-key handle created by CreateFromPem.
        // Re-import through PKCS#12 so Windows gets a non-ephemeral user-key handle suitable for SslStream.
        try
        {
            byte[] pkcs12 = certificate.Export(X509ContentType.Pkcs12);
            try
            {
                return X509CertificateLoader.LoadPkcs12(pkcs12, password: null, X509KeyStorageFlags.UserKeySet);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(pkcs12);
            }
        }
        finally
        {
            certificate.Dispose();
        }
    }
}
