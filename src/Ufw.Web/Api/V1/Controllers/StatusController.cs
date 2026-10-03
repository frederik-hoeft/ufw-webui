using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Status;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class StatusController(IStatusDaemonGateway daemonStatus) : ControllerBase
{
    public async partial Task<IActionResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        DaemonResult daemonResult = await daemonStatus.GetStatusAsync(cancellationToken);
        daemonResult.EnsureSuccess();
        return NoContent();
    }
}
