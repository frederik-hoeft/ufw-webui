using Ufw.Roslyn.Controllers.Routing;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/intent")]
internal sealed partial class IntentController
{
    /// <summary>
    /// Returns the daemon deployment identity and supported signed-intent protocol version.
    /// </summary>
    [Get("context")]
    public partial ValueTask<IntentContextResponse> GetContextAsync(CancellationToken cancellationToken);
}
