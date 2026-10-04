using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Api.V1.Mapping;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.Rules.Intent;
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
            RuleMetadataUpdateOutcome.RuleNotFound => NotFound(ApiProblemDetailsFactory.Create(
                StatusCodes.Status404NotFound,
                title: "Firewall rule not found",
                detail: "The referenced firewall rule does not exist.")),
            RuleMetadataUpdateOutcome.TagNotFound => BadRequest(ApiProblemDetailsFactory.Create(
                StatusCodes.Status400BadRequest,
                title: "Rule metadata tag does not exist",
                detail: "One or more referenced rule tags do not exist.")),
            RuleMetadataUpdateOutcome.GroupNotFound => BadRequest(ApiProblemDetailsFactory.Create(
                StatusCodes.Status400BadRequest,
                title: "Rule metadata group does not exist",
                detail: "The referenced rule group does not exist.")),
            RuleMetadataUpdateOutcome.DependencyChanged => BadRequest(ApiProblemDetailsFactory.Create(
                StatusCodes.Status400BadRequest,
                title: "Rule metadata dependencies changed",
                detail: "Rule metadata dependencies changed.")),
            _ => throw new InvalidOperationException($"Unknown rule metadata update outcome '{result.Outcome}'."),
        };
    }

    public async partial Task<ActionResult<RuleMutationResponse>> AddRuleAsync(AddRuleIntentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AddRuleRequest daemonRequest = request.ToDaemonRequest();
        DaemonResult<RuleMutationResponse> daemonResult = await daemonRules.AddRuleAsync(daemonRequest, cancellationToken);
        RuleMutationResponse response = daemonResult.Result;
        return Ok(response);
    }

    public async partial Task<ActionResult<RuleInsertionResponse>> InsertRuleAsync(InsertRuleIntentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        InsertRuleRequest daemonRequest = request.ToDaemonRequest();
        DaemonResult<RuleInsertionResponse> daemonResult = await daemonRules.InsertRuleAsync(daemonRequest, cancellationToken);
        RuleInsertionResponse response = daemonResult.Result;
        return InsertionResult(response);
    }

    public async partial Task<ActionResult<RuleReplacementMutationResponse>> ReplaceRuleAsync(ReplaceRuleIntentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ReplaceRuleRequest daemonRequest = request.ToDaemonRequest();
        DaemonResult<RuleReplacementExecutionResult> daemonResult = await daemonRules.ReplaceRuleAsync(daemonRequest, cancellationToken);
        RuleReplacementExecutionResult replacement = daemonResult.Result;
        RuleReplacementMetadataReconciliationOutcome metadataOutcome;
        switch (replacement.Reconciliation)
        {
            case RuleReplacementReconciliationReady ready:
                metadataOutcome = await metadata.ReconcileReplacementAsync(ready.Facts, CancellationToken.None);
                break;
            case RuleReplacementReconciliationNotRequired:
                metadataOutcome = RuleReplacementMetadataReconciliationOutcome.NotAttempted;
                break;
            case RuleReplacementReconciliationPreparationFailed:
                metadataOutcome = RuleReplacementMetadataReconciliationOutcome.Failed;
                break;
            default:
                throw new InvalidOperationException($"Unknown replacement reconciliation plan '{replacement.Reconciliation.GetType().Name}'.");
        }
        RuleReplacementMutationResponse response = new(
            replacement.Firewall,
            metadataOutcome,
            metadataOutcome == RuleReplacementMetadataReconciliationOutcome.Failed ? METADATA_RECONCILIATION_FAILURE_DIAGNOSTIC : null);
        return ReplacementResult(response);
    }

    public async partial Task<ActionResult<RuleReorderResponse>> ReorderRulesAsync(ReorderRulesIntentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ReorderRulesRequest daemonRequest = request.ToDaemonRequest();
        DaemonResult<RuleReorderResponse> daemonResult = await daemonRules.ReorderRulesAsync(daemonRequest, cancellationToken);
        RuleReorderResponse response = daemonResult.Result;
        return ReorderResult(response);
    }

    public async partial Task<ActionResult<RuleBatchDeleteResponse>> BatchDeleteRulesAsync(BatchDeleteRulesIntentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        BatchDeleteRulesRequest daemonRequest = request.ToDaemonRequest();
        DaemonResult<RuleBatchDeleteResponse> daemonResult = await daemonRules.BatchDeleteRulesAsync(daemonRequest, cancellationToken);
        RuleBatchDeleteResponse response = daemonResult.Result;
        await metadata.ReconcileBatchDeleteAsync(response, CancellationToken.None);
        return BatchDeleteResult(response);
    }

    public async partial Task<ActionResult<RuleMutationResponse>> DeleteRuleAsync(DeleteRuleIntentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DeleteRuleRequest daemonRequest = request.ToDaemonRequest();
        DaemonResult<RuleMutationResponse> daemonResult = await daemonRules.DeleteRuleAsync(daemonRequest, cancellationToken);
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
