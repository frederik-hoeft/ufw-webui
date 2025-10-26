using System.Security.Cryptography.X509Certificates;

namespace Ufw.Ipc.Shared.Security.Certificates;

public interface ICertificateLoader
{
    ValueTask<X509Certificate2> LoadCertificateAsync(string certificatePath, string? certificateKeyPath, CancellationToken cancellationToken = default);
}
