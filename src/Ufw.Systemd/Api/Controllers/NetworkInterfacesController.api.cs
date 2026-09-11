using Ufw.Roslyn.Controllers.Routing;
using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/network-interfaces")]
internal sealed partial class NetworkInterfacesController
{
    /// <summary>
    /// Returns the host network-interface names visible to the daemon.
    /// </summary>
    [Get]
    public partial ValueTask<IResponsePayload> GetNetworkInterfacesAsync(CancellationToken cancellationToken);
}
