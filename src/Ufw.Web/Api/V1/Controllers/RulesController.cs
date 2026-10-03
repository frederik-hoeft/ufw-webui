using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RulesController(IRuleDaemonGateway daemonRules, IRuleInventoryService inventory, IRuleMetadataService metadata) : ControllerBase
{
    private const string METADATA_RECONCILIATION_FAILURE_DIAGNOSTIC =
        "Firewall rule replacement completed, but application metadata reconciliation failed. "
        + "Reload the rule inventory before making further metadata changes.";

    public async partial Task<ActionResult<RuleInventoryResponse>> GetRulesAsync(CancellationToken cancellationToken)
    {
        RuleInventoryResponse response = await inventory.GetAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<ActionResult<RuleMetadataMutationResponse>> UpdateMetadataAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
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

    public async partial Task<ActionResult<RuleMutationResponse>> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.ADD_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.add'." });
        }

        DaemonResult<RuleMutationResponse> daemonResult = await daemonRules.AddRuleAsync(request, cancellationToken);
        RuleMutationResponse response = daemonResult.Result;
        return Ok(response);
    }

    public async partial Task<ActionResult<RuleInsertionResponse>> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.INSERT_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.insert'." });
        }

        DaemonResult<RuleInsertionResponse> daemonResult = await daemonRules.InsertRuleAsync(request, cancellationToken);
        RuleInsertionResponse response = daemonResult.Result;
        return InsertionResult(response);
    }

    public async partial Task<ActionResult<RuleReplacementMutationResponse>> ReplaceRuleAsync(ReplaceRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.REPLACE_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.replace'." });
        }

        DaemonResult<RuleReplacementResponse> daemonResult = await daemonRules.ReplaceRuleAsync(request, cancellationToken);
        RuleReplacementResponse firewall = daemonResult.Result;
        RuleReplacementMetadataReconciliationOutcome metadataOutcome;
        if (firewall.Outcome == RuleReplacementOutcome.Completed)
        {
            metadataOutcome = await metadata.ReconcileReplacementAsync(request, firewall, CancellationToken.None);
        }
        else
        {
            metadataOutcome = RuleReplacementMetadataReconciliationOutcome.NotAttempted;
        }
        RuleReplacementMutationResponse response = new(
            firewall,
            metadataOutcome,
            metadataOutcome == RuleReplacementMetadataReconciliationOutcome.Failed ? METADATA_RECONCILIATION_FAILURE_DIAGNOSTIC : null);
        return ReplacementResult(response);
    }

    public async partial Task<ActionResult<RuleReorderResponse>> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.REORDER_RULES, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.reorder'." });
        }

        DaemonResult<RuleReorderResponse> daemonResult = await daemonRules.ReorderRulesAsync(request, cancellationToken);
        RuleReorderResponse response = daemonResult.Result;
        return ReorderResult(response);
    }

    public async partial Task<ActionResult<RuleBatchDeleteResponse>> BatchDeleteRulesAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.DELETE_RULES_BATCH, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.delete-batch'." });
        }

        DaemonResult<RuleBatchDeleteResponse> daemonResult = await daemonRules.BatchDeleteRulesAsync(request, cancellationToken);
        RuleBatchDeleteResponse response = daemonResult.Result;
        await metadata.ReconcileBatchDeleteAsync(response, CancellationToken.None);
        return BatchDeleteResult(response);
    }

    public async partial Task<ActionResult<RuleMutationResponse>> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Operation, IntentOperations.DELETE_RULE, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Request operation must be 'rules.delete'." });
        }

        DaemonResult<RuleMutationResponse> daemonResult = await daemonRules.DeleteRuleAsync(request, cancellationToken);
        RuleMutationResponse response = daemonResult.Result;
        if (!string.IsNullOrWhiteSpace(response.Rule.RuleId))
        {
            await metadata.RemoveForDeletedRuleAsync(response.Rule.RuleId, CancellationToken.None);
        }
        return Ok(response);
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
}
