using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Services.Status;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class StatusController(IStatusDaemonGateway daemonStatus, IDaemonApiErrorMapper daemonErrors) : ControllerBase
{
    public async partial Task<IActionResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            await daemonStatus.GetStatusAsync(cancellationToken);
            return NoContent();
        }
        catch (UfwIpcException exception)
        {
            DaemonApiError error = daemonErrors.MapProxyFailure(exception);
            return StatusCode(error.StatusCode, error.Problem);
        }
    }
}
