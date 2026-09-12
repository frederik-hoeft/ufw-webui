using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Api.V1.Errors;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RulesController(IUfwClient ufwClient, IDaemonApiErrorMapper daemonErrors) : ControllerBase
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

    public async partial Task<ActionResult<RuleReorderResponse>> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.REORDER_RULES, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.reorder'." });
        }

        try
        {
            RuleReorderResponse response = await ufwClient.SendAsync<ReorderRulesRequest, RuleReorderResponse>(request, cancellationToken);
            return ReorderResult(response);
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

    private ActionResult<RuleReorderResponse> ReorderResult(RuleReorderResponse response)
    {
        int statusCode = response.Outcome switch
        {
            RuleReorderOutcome.Completed => StatusCodes.Status200OK,
            RuleReorderOutcome.StaleBaseline => StatusCodes.Status409Conflict,
            RuleReorderOutcome.PreconditionFailed => StatusCodes.Status422UnprocessableEntity,
            RuleReorderOutcome.PartiallyCompleted => StatusCodes.Status409Conflict,
            RuleReorderOutcome.RecoveryFailed => StatusCodes.Status500InternalServerError,
            RuleReorderOutcome.StateUncertain => StatusCodes.Status503ServiceUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(response), response.Outcome, "Unknown reorder outcome."),
        };
        return StatusCode(statusCode, response);
    }

    private ObjectResult MapDaemonError(UfwIpcException exception)
    {
        DaemonApiError error = daemonErrors.MapProxyFailure(exception);
        return StatusCode(error.StatusCode, error.Problem);
    }
}
