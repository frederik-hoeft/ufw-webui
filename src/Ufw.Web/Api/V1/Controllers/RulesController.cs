using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RulesController(IUfwClient ufwClient) : ControllerBase
{
    public async partial Task<ActionResult<RuleListResponse>> GetRulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            RuleListResponse response = await ufwClient.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    public async partial Task<ActionResult<RuleMutationResponse>> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.ADD_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.add'." });
        }

        try
        {
            RuleMutationResponse response = await ufwClient.SendAsync<AddRuleRequest, RuleMutationResponse>(request, cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    public async partial Task<ActionResult<RuleMutationResponse>> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.DELETE_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.delete'." });
        }

        try
        {
            RuleMutationResponse response = await ufwClient.SendAsync<DeleteRuleRequest, RuleMutationResponse>(request, cancellationToken);
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
            foreach (ModelValidationError error in exception.ValidationErrors)
            {
                details.Errors[error.PropertyName] = [error.ErrorMessage];
            }

            return ValidationProblem(details);
        }

        return Problem(statusCode: statusCode, detail: exception.ResponseMessage);
    }
}
