namespace Ufw.Ipc.Shared.Transport.Security;

public class NoTransportSecurityService : ITransportSecurityService
{
    public Task<Stream> OpenSecureStreamAsync(Stream innerStream, CancellationToken cancellationToken = default) => 
        Task.FromResult<Stream>(new TimedStream(innerStream, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));
}
