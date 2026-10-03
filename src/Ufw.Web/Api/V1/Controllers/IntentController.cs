using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Services.Intent;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class IntentController(IIntentDaemonGateway daemonIntent, IDaemonApiErrorMapper daemonErrors) : ControllerBase
{
    public async partial Task<ActionResult<IntentContextResponse>> GetContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            IntentContextResponse response = await daemonIntent.GetContextAsync(cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            DaemonApiError error = daemonErrors.MapProxyFailure(exception);
            return StatusCode(error.StatusCode, error.Problem);
        }
    }
}
