namespace Ufw.Shared.Ipc.Transport;

public interface ITransportLayerConnection : IDisposable, IAsyncDisposable
{
    Stream GetStream(TimeSpan readTimeout, TimeSpan writeTimeout);
}
