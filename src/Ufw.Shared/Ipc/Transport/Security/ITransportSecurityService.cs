namespace Ufw.Shared.Ipc.Transport.Security;

public interface ITransportSecurityService
{
    Task<Stream> OpenSecureStreamAsync(Stream innerStream, CancellationToken cancellationToken = default);
}
