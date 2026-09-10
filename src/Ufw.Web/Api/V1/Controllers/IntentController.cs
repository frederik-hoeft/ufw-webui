using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class IntentController(IUfwClient ufwClient) : ControllerBase
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
            int statusCode = exception.StatusCode is >= 400 and <= 599
                ? exception.StatusCode
                : StatusCodes.Status502BadGateway;
            return Problem(statusCode: statusCode, detail: exception.ResponseMessage);
        }
    }
}
