namespace Ufw.Pipes.Shared.Transport;

public interface ITransportLayerConnection : IDisposable, IAsyncDisposable
{
    Stream GetStream(TimeSpan readTimeout, TimeSpan writeTimeout);
}
