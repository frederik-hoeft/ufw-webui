using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Services.Intent;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class IntentController(IIntentDaemonGateway daemonIntent) : ControllerBase
{
    public async partial Task<ActionResult<IntentContextResponse>> GetContextAsync(CancellationToken cancellationToken)
    {
        IntentContextResponse response = (await daemonIntent.GetContextAsync(cancellationToken)).Result;
        return Ok(response);
    }
}
