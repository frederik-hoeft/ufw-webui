using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Systemd.Api.Controllers;

internal sealed partial class StatusController : ControllerBase
{
    public partial ValueTask<IResponsePayload> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IResponsePayload>(new OkResponse());
    }
}
