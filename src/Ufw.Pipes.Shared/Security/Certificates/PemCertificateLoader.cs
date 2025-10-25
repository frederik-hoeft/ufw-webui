using System.Security.Cryptography.X509Certificates;

namespace Ufw.Pipes.Shared.Security.Certificates;

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
        return X509Certificate2.CreateFromPem(certificatePem, keyPem);
    }
}