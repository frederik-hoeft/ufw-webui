using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses.Domain;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/intent")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IntentController(IUfwClient ufwClient) : ControllerBase
{
    [HttpGet("context")]
    [ProducesResponseType<IntentContextResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IntentContextResponse>> GetContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            IntentContextResponse response = await ufwClient.SendAsync<IntentContextResponse>(
                RequestMethod.Get,
                "/api/v1/intent/context",
                cancellationToken);
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
