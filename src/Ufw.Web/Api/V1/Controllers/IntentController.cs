using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Errors;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class IntentController(IUfwClient ufwClient, IDaemonApiErrorMapper daemonErrors) : ControllerBase
{
    public async partial Task<ActionResult<IntentContextResponse>> GetContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            IntentContextResponse response = await ufwClient.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            DaemonApiError error = daemonErrors.MapProxyFailure(exception);
            return StatusCode(error.StatusCode, error.Problem);
        }
    }
}
