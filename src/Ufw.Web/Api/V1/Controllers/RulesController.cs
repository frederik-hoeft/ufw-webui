using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RulesController(IRuleDaemonGateway daemonRules, IRuleInventoryService inventory, IRuleMetadataService metadata, IDaemonApiErrorMapper daemonErrors) : ControllerBase
{
    private const string METADATA_RECONCILIATION_FAILURE_DIAGNOSTIC =
        "Firewall rule replacement completed, but application metadata reconciliation failed. "
        + "Reload the rule inventory before making further metadata changes.";

    public async partial Task<ActionResult<RuleInventoryResponse>> GetRulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            RuleInventoryResponse response = await inventory.GetAsync(cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    public async partial Task<ActionResult<RuleMetadataMutationResponse>> UpdateMetadataAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            RuleMetadataUpdateResult result = await metadata.UpdateAsync(ruleId, request, cancellationToken);
            return result.Outcome switch
            {
                RuleMetadataUpdateOutcome.Success => Ok(result.Response),
                RuleMetadataUpdateOutcome.RuleNotFound => NotFound(),
                RuleMetadataUpdateOutcome.TagNotFound => BadRequest(new { message = "One or more referenced rule tags do not exist." }),
                RuleMetadataUpdateOutcome.GroupNotFound => BadRequest(new { message = "The referenced rule group does not exist." }),
                RuleMetadataUpdateOutcome.InvalidMetadata => BadRequest(new { message = "Rule metadata is invalid." }),
                _ => throw new InvalidOperationException($"Unknown rule metadata update outcome '{result.Outcome}'."),
            };
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
            RuleMutationResponse response = await daemonRules.AddRuleAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    public async partial Task<ActionResult<RuleInsertionResponse>> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.INSERT_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.insert'." });
        }

        try
        {
            RuleInsertionResponse response = await daemonRules.InsertRuleAsync(request, cancellationToken);
            return InsertionResult(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    public async partial Task<ActionResult<RuleReplacementMutationResponse>> ReplaceRuleAsync(ReplaceRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.REPLACE_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.replace'." });
        }

        try
        {
            RuleReplacementResponse firewall = await daemonRules.ReplaceRuleAsync(request, cancellationToken);
            RuleReplacementMetadataReconciliationOutcome metadataOutcome = firewall.Outcome == RuleReplacementOutcome.Completed
                ? await metadata.ReconcileReplacementAsync(request, firewall, CancellationToken.None)
                : RuleReplacementMetadataReconciliationOutcome.NotAttempted;
            RuleReplacementMutationResponse response = new(
                firewall,
                metadataOutcome,
                metadataOutcome == RuleReplacementMetadataReconciliationOutcome.Failed ? METADATA_RECONCILIATION_FAILURE_DIAGNOSTIC : null);
            return ReplacementResult(response);
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
            RuleReorderResponse response = await daemonRules.ReorderRulesAsync(request, cancellationToken);
            return ReorderResult(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    public async partial Task<ActionResult<RuleBatchDeleteResponse>> BatchDeleteRulesAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.DELETE_RULES_BATCH, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.delete-batch'." });
        }

        try
        {
            RuleBatchDeleteResponse response = await daemonRules.BatchDeleteRulesAsync(request, cancellationToken);
            await metadata.ReconcileBatchDeleteAsync(response, CancellationToken.None);
            return BatchDeleteResult(response);
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
            RuleMutationResponse response = await daemonRules.DeleteRuleAsync(request, cancellationToken);
            if (!string.IsNullOrWhiteSpace(response.Rule.RuleId))
            {
                await metadata.RemoveForDeletedRuleAsync(response.Rule.RuleId, CancellationToken.None);
            }
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    private ActionResult<RuleReplacementMutationResponse> ReplacementResult(RuleReplacementMutationResponse response)
    {
        int statusCode = response.MetadataReconciliation == RuleReplacementMetadataReconciliationOutcome.Failed
            ? StatusCodes.Status500InternalServerError
            : response.Firewall.Outcome switch
            {
                RuleReplacementOutcome.Completed => StatusCodes.Status200OK,
                RuleReplacementOutcome.StaleBaseline => StatusCodes.Status409Conflict,
                RuleReplacementOutcome.PreconditionFailed => StatusCodes.Status422UnprocessableEntity,
                RuleReplacementOutcome.PartiallyCompleted => StatusCodes.Status409Conflict,
                RuleReplacementOutcome.StateUncertain => StatusCodes.Status503ServiceUnavailable,
                _ => throw new ArgumentOutOfRangeException(nameof(response), response.Firewall.Outcome, "Unknown replacement outcome."),
            };
        return StatusCode(statusCode, response);
    }

    private ActionResult<RuleInsertionResponse> InsertionResult(RuleInsertionResponse response)
    {
        int statusCode = response.Outcome switch
        {
            RuleInsertionOutcome.Completed => StatusCodes.Status200OK,
            RuleInsertionOutcome.StaleBaseline => StatusCodes.Status409Conflict,
            RuleInsertionOutcome.PreconditionFailed => StatusCodes.Status422UnprocessableEntity,
            RuleInsertionOutcome.StateUncertain => StatusCodes.Status503ServiceUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(response), response.Outcome, "Unknown insertion outcome."),
        };
        return StatusCode(statusCode, response);
    }

    private ActionResult<RuleBatchDeleteResponse> BatchDeleteResult(RuleBatchDeleteResponse response)
    {
        int statusCode = response.Outcome switch
        {
            RuleBatchDeleteOutcome.Completed => StatusCodes.Status200OK,
            RuleBatchDeleteOutcome.StaleBaseline => StatusCodes.Status409Conflict,
            RuleBatchDeleteOutcome.PreconditionFailed => StatusCodes.Status422UnprocessableEntity,
            RuleBatchDeleteOutcome.PartiallyCompleted => StatusCodes.Status409Conflict,
            RuleBatchDeleteOutcome.StateUncertain => StatusCodes.Status503ServiceUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(response), response.Outcome, "Unknown batch-delete outcome."),
        };
        return StatusCode(statusCode, response);
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
