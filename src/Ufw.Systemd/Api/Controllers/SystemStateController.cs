using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model;
using Ufw.Systemd.Firewall.SystemState;

namespace Ufw.Systemd.Api.Controllers;

internal sealed partial class SystemStateController(IFilterSystemStateQueryService systemState) : ControllerBase
{
    public partial ValueTask<IResponsePayload> GetSystemStateAsync(CancellationToken cancellationToken) =>
        systemState.GetAsync(cancellationToken);
}
