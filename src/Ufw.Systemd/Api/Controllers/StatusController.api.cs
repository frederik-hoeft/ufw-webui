using Ufw.Roslyn.Controllers.Routing;
using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/status")]
internal sealed partial class StatusController
{
    /// <summary>
    /// Performs a daemon-process liveness probe without reading UFW or persistent daemon state.
    /// </summary>
    [Get]
    public partial ValueTask<IResponsePayload> GetStatusAsync(CancellationToken cancellationToken);
}
