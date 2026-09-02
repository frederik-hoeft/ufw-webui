using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Model.Responses.Domain;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/rules")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class RulesController(IUfwClient ufwClient) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<RuleListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<RuleListResponse>> GetRulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            RuleListResponse response = await ufwClient.SendAsync<RuleListResponse>(
                RequestMethod.Get,
                "/api/v1/rules",
                cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    [HttpPost]
    [ProducesResponseType<RuleMutationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleMutationResponse>> AddRuleAsync(
        [FromBody] AddRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Operation, Ufw.Ipc.Shared.Security.Intent.IntentOperations.ADD_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.add'." });
        }

        try
        {
            RuleMutationResponse response = await ufwClient.SendAsync<AddRuleRequest, RuleMutationResponse>(
                request,
                cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    [HttpDelete]
    [ProducesResponseType<RuleMutationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleMutationResponse>> DeleteRuleAsync(
        [FromBody] DeleteRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Operation, Ufw.Ipc.Shared.Security.Intent.IntentOperations.DELETE_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.delete'." });
        }

        try
        {
            RuleMutationResponse response = await ufwClient.SendAsync<DeleteRuleRequest, RuleMutationResponse>(
                request,
                cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    private ActionResult MapDaemonError(UfwIpcException exception)
    {
        int statusCode = exception.StatusCode is >= 400 and <= 599
            ? exception.StatusCode
            : StatusCodes.Status502BadGateway;

        if (exception.ValidationErrors is { Length: > 0 })
        {
            ValidationProblemDetails details = new()
            {
                Status = StatusCodes.Status400BadRequest,
                Title = exception.ResponseMessage ?? "One or more validation errors occurred.",
            };
            foreach (Ufw.Ipc.Shared.Model.Responses.ModelValidationError error in exception.ValidationErrors)
            {
                details.Errors[error.PropertyName] = [error.ErrorMessage];
            }

            return ValidationProblem(details);
        }

        return Problem(statusCode: statusCode, detail: exception.ResponseMessage);
    }
}
