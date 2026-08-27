using Ufw.Ipc.Client.Transport;
using Ufw.Ipc.Shared.Transport;

namespace Ufw.Ipc.Tests.Adapter.Transport;

/// <summary>
/// Client-side <see cref="ITransportLayerService"/> that dials the shared in-process broker.
/// </summary>
internal sealed class InProcessClientTransportService(InProcessTransportBroker broker) : ITransportLayerService
{
    public Task<ITransportLayerConnection> ConnectAsync(CancellationToken cancellationToken) =>
        broker.ConnectAsync(cancellationToken).AsTask();
}
