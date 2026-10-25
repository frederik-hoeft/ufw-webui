using Ufw.Roslyn.Controllers.Routing;
using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/system-state")]
internal sealed partial class SystemStateController
{
    /// <summary>
    /// Returns the read-only structural snapshot of the active IPv4 and IPv6 filter tables.
    /// </summary>
    [Get]
    public partial ValueTask<IResponsePayload> GetSystemStateAsync(CancellationToken cancellationToken);
}
