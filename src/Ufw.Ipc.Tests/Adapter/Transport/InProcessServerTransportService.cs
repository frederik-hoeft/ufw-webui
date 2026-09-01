using Ufw.Ipc.Shared.Transport;
using Ufw.Systemd.Transport;

namespace Ufw.Ipc.Tests.Adapter.Transport;

/// <summary>
/// Server-side <see cref="ITransportLayerService"/> that accepts connections from the shared in-process broker.
/// </summary>
internal sealed class InProcessServerTransportService(InProcessTransportBroker broker) : ITransportLayerService
{
    public Task<ITransportLayerConnection> ServeAsync(CancellationToken cancellationToken) =>
        broker.AcceptAsync(cancellationToken).AsTask();
}
