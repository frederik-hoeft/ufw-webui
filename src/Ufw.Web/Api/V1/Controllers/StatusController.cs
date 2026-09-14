using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Web.Api.V1.Errors;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class StatusController(IUfwClient ufwClient, IDaemonApiErrorMapper daemonErrors) : ControllerBase
{
    public async partial Task<IActionResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ufwClient.SendAsync(RequestMethod.Get, "/api/v1/status", cancellationToken);
            return NoContent();
        }
        catch (UfwIpcException exception)
        {
            DaemonApiError error = daemonErrors.MapProxyFailure(exception);
            return StatusCode(error.StatusCode, error.Problem);
        }
    }
}
